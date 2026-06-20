using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Domain.TechLog;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Сервис жизненного цикла сбора ТЖ режима «Расследование» (MLC-230/231, ADR-57/58). Зеркаль
// PerfRecordingService: singleton, сериализует операции через один SemaphoreSlim, БД — через
// IServiceScopeFactory (scoped AppDbContext + IAuditLogger), время — через TimeProvider. Побочный
// эффект — файл logcfg.xml в conf платформы (через ILogcfgStore), поэтому установка делает пробу
// прав, бэкап исходного и запись, а снятие восстанавливает исходный. Аудит — только при фактическом
// успехе (806/807/808). Безопасный сбор (MLC-231, 60_SAFETY №3/№4): single-active по БД + сторож
// места перед стартом (InstallAsync); периодический сторож активного дела (MonitorActiveAsync) —
// авто-стоп по окну времени (TimeLimit) и лимиту места (DiskLimit); orphan-recovery на старте
// (RecoverInterruptedAsync, Active→Interrupted). Драйвер тайминга/порядка старта — TechLogWatchdogService.
internal sealed partial class TechLogCollectionService : ITechLogCollectionService, IDisposable
{
    private const string DefaultCollectionRoot = @"%PROGRAMDATA%\MitLicenseCenter\techlog";
    private const int DefaultHistoryHours = 2;
    private const int DefaultMaxDurationMinutes = 10;
    private const int DefaultDiskLimitMb = 2048;
    private const int DefaultMinFreeDiskMb = 1024;
    private const long BytesPerMb = 1024L * 1024L;
    private const string SystemInitiator = "system";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogcfgBuilder _builder;
    private readonly ILogcfgStore _store;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<TechLogCollectionService> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ActiveState? _active;
    private volatile bool _hasActive;

    public TechLogCollectionService(
        IServiceScopeFactory scopeFactory,
        ILogcfgBuilder builder,
        ILogcfgStore store,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ILogger<TechLogCollectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _builder = builder;
        _store = store;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public bool HasActiveCollection => _hasActive;

    public async Task<TechLogStartResult> InstallAsync(
        string startedBy, TechLogScenario scenario, string? infobaseProcessName, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Один активный сбор за раз — сначала дешёвый in-memory стейт (как PerfRecordingService).
            if (_active is { } current)
            {
                return new TechLogStartResult(TechLogStartOutcome.AlreadyActive, current.Id);
            }

            var path = _store.ResolveLogcfgPath();
            if (path is null)
            {
                return new TechLogStartResult(
                    TechLogStartOutcome.RootNotFound, Guid.Empty,
                    Issue: "Не найден каталог conf платформы 1С (…\\1cv8\\conf). Проверьте установку 1С на узле.");
            }

            // Single-active по БД (60_SAFETY №4) ДО записи logcfg: закрывает случай потери in-memory
            // стейта после рестарта (orphan-recovery на старте переведёт осиротевшее дело в Interrupted,
            // но если он ещё не отработал — БД-проверка не даёт поставить второй конфиг).
            using (var checkScope = _scopeFactory.CreateScope())
            {
                var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var existing = await checkDb.TechLogCollections
                    .FirstOrDefaultAsync(c => c.Status == TechLogCollectionStatus.Active, ct)
                    .ConfigureAwait(false);
                if (existing is not null)
                {
                    return new TechLogStartResult(TechLogStartOutcome.AlreadyActive, existing.Id);
                }
            }

            // Проба прав ДО генерации/записи: нет прав — отдаём оператору точную команду icacls
            // (зеркаль RAS-healing), состояние не трогаем.
            var probe = _store.ProbeWriteAccess();
            if (!probe.CanWrite)
            {
                return new TechLogStartResult(
                    TechLogStartOutcome.NoWriteAccess, Guid.Empty,
                    GrantCommand: probe.GrantCommand, Issue: probe.Issue);
            }

            var collectionDir = ResolveCollectionDirectory();

            // Сторож места ДО старта (60_SAFETY №3): свободного места < порога — НЕ стартуем. Лимит
            // места критичнее обычного (полный ТЖ забивает диск за минуты, фильтр длительности объём
            // не страхует, MLC-229). null от пробы (том не определить) = «проверка невозможна» — не
            // блокируем (отказ только при ЗАВЕДОМОЙ нехватке, как disk-guard бэкапа).
            var minFreeMb = ResolveSetting(SettingKey.TechLogMinFreeDiskMb, DefaultMinFreeDiskMb);
            if (minFreeMb > 0)
            {
                var freeBytes = _store.GetAvailableFreeSpaceBytes(collectionDir);
                if (freeBytes is { } free && free < (long)minFreeMb * BytesPerMb)
                {
                    var freeMb = free / BytesPerMb;
                    return new TechLogStartResult(
                        TechLogStartOutcome.InsufficientDiskSpace, Guid.Empty,
                        Issue: $"Недостаточно свободного места для сбора технологического журнала: " +
                               $"свободно {freeMb} МБ, требуется не менее {minFreeMb} МБ. " +
                               "Освободите место или измените порог в «Параметрах» (TechLog.MinFreeDiskMb).");
                }
            }

            var historyHours = ResolveHistoryHours();
            var content = _builder.Build(scenario, infobaseProcessName, collectionDir, historyHours);

            // Бэкап исходного + запись целевого. Каталог сбора создаём (платформа пишет ТЖ под
            // аккаунтом агента — ACL каталога настраивается на этапе C/MLC-231).
            Directory.CreateDirectory(collectionDir);
            _store.WriteLogcfg(content);

            var id = Guid.NewGuid();
            var now = _clock.GetUtcNow().UtcDateTime;
            var collection = new TechLogCollection
            {
                Id = id,
                Status = TechLogCollectionStatus.Active,
                StartedAtUtc = now,
                Scenario = scenario.ToString(),
                InfobaseProcessName = infobaseProcessName,
                CollectionDirectory = collectionDir,
                ConfigMarker = LogcfgBuilder.Marker,
            };

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            db.TechLogCollections.Add(collection);

            var initiator = string.IsNullOrWhiteSpace(startedBy) ? "Unknown" : startedBy;
            var scopeText = infobaseProcessName is null ? "весь кластер" : $"ИБ {infobaseProcessName}";
            audit.Enlist(
                AuditActionType.TechLogCollectionStarted,
                initiator,
                $"Запущен сбор технологического журнала: сценарий {scenario}, {scopeText}.");
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            _active = new ActiveState(id, now, collectionDir);
            _hasActive = true;
            LogInstalled(_logger, id, scenario, infobaseProcessName ?? "*");
            return new TechLogStartResult(TechLogStartOutcome.Started, id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TechLogStopOutcome> RemoveAsync(
        Guid collectionId, TechLogCollectionStopReason reason, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_active is null || _active.Id != collectionId)
            {
                return TechLogStopOutcome.NotActive;
            }

            await FinalizeActiveLockedAsync(reason, ct).ConfigureAwait(false);
            return TechLogStopOutcome.Stopped;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MonitorActiveAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_active is not { } active)
            {
                return;
            }

            // Авто-стоп по окну времени (60_SAFETY №3): прошло ≥ TechLog.MaxDurationMinutes от старта.
            var maxDurationMinutes = ResolveSetting(SettingKey.TechLogMaxDurationMinutes, DefaultMaxDurationMinutes);
            var now = _clock.GetUtcNow().UtcDateTime;
            if (now - active.StartedAtUtc >= TimeSpan.FromMinutes(maxDurationMinutes))
            {
                await FinalizeActiveLockedAsync(TechLogCollectionStopReason.TimeLimit, ct).ConfigureAwait(false);
                return;
            }

            // Авто-стоп по лимиту места (60_SAFETY №3): размер каталога сбора ≥ TechLog.DiskLimitMb.
            // Размер — за seam'ом store (тест симулирует превышение без файлов).
            var diskLimitMb = ResolveSetting(SettingKey.TechLogDiskLimitMb, DefaultDiskLimitMb);
            var sizeBytes = _store.GetDirectorySizeBytes(active.CollectionDirectory);
            if (sizeBytes >= (long)diskLimitMb * BytesPerMb)
            {
                await FinalizeActiveLockedAsync(TechLogCollectionStopReason.DiskLimit, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Тик сторожа best-effort: сбой логируем, дело остаётся активным — следующий тик повторит.
            LogMonitorFailed(_logger, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecoverInterruptedAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Зеркаль PerfRecording.RecoverInterruptedAsync: после рестарта процесса все Active →
            // Interrupted (in-memory стейт потерян, дело не «остановлено по причине», а оборвано).
            // logcfg при этом снимет стартовая сверка файла (ReconcileOnStartupAsync), которую драйвер
            // зовёт ПОСЛЕ этого метода.
            var now = _clock.GetUtcNow().UtcDateTime;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var orphaned = await db.TechLogCollections
                .Where(c => c.Status == TechLogCollectionStatus.Active)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (orphaned.Count == 0)
            {
                return;
            }

            foreach (var collection in orphaned)
            {
                collection.Status = TechLogCollectionStatus.Interrupted;
                collection.StoppedAtUtc = now;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            LogRecovered(_logger, orphaned.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogRecoverFailed(_logger, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Закрывает активное дело (вызывается под _gate): восстановление исходного logcfg → дело Stopped
    // (reason) → аудит. Восстановление конфига — главная гарантия (60_SAFETY №5): даже если БД-запись
    // упадёт, конфиг уже снят. Идемпотентно: повторный restore без бэкапа — no-op.
    private async Task FinalizeActiveLockedAsync(TechLogCollectionStopReason reason, CancellationToken ct)
    {
        var active = _active!;
        _store.RestoreOriginal();

        var now = _clock.GetUtcNow().UtcDateTime;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

        var collection = await db.TechLogCollections
            .FirstOrDefaultAsync(c => c.Id == active.Id, ct)
            .ConfigureAwait(false);
        if (collection is not null)
        {
            collection.Status = TechLogCollectionStatus.Stopped;
            collection.StopReason = reason;
            collection.StoppedAtUtc = now;
            audit.Enlist(
                AuditActionType.TechLogCollectionStopped,
                SystemInitiator,
                $"Сбор технологического журнала остановлен ({reason}).");
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        _active = null;
        _hasActive = false;
        LogRemoved(_logger, active.Id, reason);
    }

    public async Task ReconcileOnStartupAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Лежит ли в conf НАШ logcfg (по маркеру)? Чужой/отсутствует — нечего сверять.
            var actual = _store.ReadLogcfg();
            if (!_builder.IsManaged(actual))
            {
                return;
            }

            // Наш конфиг есть. Есть ли активное дело в БД? При крахе ОС целиком (не процесса панели)
            // logcfg остаётся, а активного дела может не быть → принудительно снять «забытый» конфиг.
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            var hasActive = await db.TechLogCollections
                .AnyAsync(c => c.Status == TechLogCollectionStatus.Active, ct)
                .ConfigureAwait(false);
            if (hasActive)
            {
                // Есть активное дело — конфиг штатный, не трогаем. Драйвер зовёт RecoverInterruptedAsync
                // ДО этого метода (Active→Interrupted), поэтому осиротевшее после рестарта дело сюда уже
                // не попадёт; активным остаётся лишь то, что переживёт перевод (в норме — пусто, in-memory
                // стейт после рестарта потерян). Нечего снимать.
                return;
            }

            _store.RestoreOriginal();
            audit.Enlist(
                AuditActionType.TechLogConfigForceRestored,
                SystemInitiator,
                "Сторож на старте: обнаружен «забытый» logcfg.xml панели без активного дела — исходный конфиг восстановлен.");
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            LogForceRestored(_logger);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogReconcileFailed(_logger, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    // Каталог сбора (location). %ENV% раскрываем; пусто → дефолт под %PROGRAMDATA%.
    private string ResolveCollectionDirectory()
    {
        var raw = _settings.GetString(SettingKey.TechLogCollectionRoot);
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = DefaultCollectionRoot;
        }

        return Environment.ExpandEnvironmentVariables(raw);
    }

    // history (часы). Клампим к whitelist-диапазону; пусто → дефолт.
    private int ResolveHistoryHours()
    {
        var value = _settings.GetInt(SettingKey.TechLogHistoryHours) ?? DefaultHistoryHours;
        return SettingDefinitions.ClampToRange(SettingKey.TechLogHistoryHours, value);
    }

    // Числовая настройка, склампленная к whitelist-диапазону; пусто → дефолт (как ResolveHistoryHours).
    private int ResolveSetting(string key, int fallback)
    {
        var value = _settings.GetInt(key) ?? fallback;
        return SettingDefinitions.ClampToRange(key, value);
    }

    // Снимок активного дела в памяти (id + старт для окна времени + каталог для лимита места).
    private sealed class ActiveState(Guid id, DateTime startedAtUtc, string collectionDirectory)
    {
        public Guid Id { get; } = id;
        public DateTime StartedAtUtc { get; } = startedAtUtc;
        public string CollectionDirectory { get; } = collectionDirectory;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Tech-log collection {CollectionId} installed (scenario {Scenario}, infobase {Infobase})")]
    private static partial void LogInstalled(ILogger logger, Guid collectionId, TechLogScenario scenario, string infobase);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Tech-log collection {CollectionId} removed ({Reason})")]
    private static partial void LogRemoved(ILogger logger, Guid collectionId, TechLogCollectionStopReason reason);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Startup watchdog force-restored a forgotten managed logcfg.xml (no active tech-log collection)")]
    private static partial void LogForceRestored(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tech-log startup reconcile failed")]
    private static partial void LogReconcileFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Recovered {Count} interrupted tech-log collection(s) on startup")]
    private static partial void LogRecovered(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tech-log interrupted-recovery failed")]
    private static partial void LogRecoverFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tech-log active-collection monitor tick failed")]
    private static partial void LogMonitorFailed(ILogger logger, Exception ex);
}
