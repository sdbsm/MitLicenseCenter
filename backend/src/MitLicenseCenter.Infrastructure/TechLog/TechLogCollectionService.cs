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

// Сервис жизненного цикла сбора ТЖ режима «Расследование» (MLC-230, ADR-57/58). Зеркаль
// PerfRecordingService: singleton, сериализует операции через один SemaphoreSlim, БД — через
// IServiceScopeFactory (scoped AppDbContext + IAuditLogger), время — через TimeProvider. Побочный
// эффект — файл logcfg.xml в conf платформы (через ILogcfgStore), поэтому установка делает пробу
// прав, бэкап исходного и запись, а снятие восстанавливает исходный. Аудит — только при фактическом
// успехе (806/807/808). Окно/авто-стоп/лимит диска/один-активный/orphan-recovery — задача MLC-231;
// здесь установка, снятие и стартовая сверка файла сторожем (ReconcileOnStartupAsync).
internal sealed partial class TechLogCollectionService : ITechLogCollectionService, IDisposable
{
    private const string DefaultCollectionRoot = @"%PROGRAMDATA%\MitLicenseCenter\techlog";
    private const int DefaultHistoryHours = 2;
    private const string SystemInitiator = "system";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogcfgBuilder _builder;
    private readonly ILogcfgStore _store;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<TechLogCollectionService> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Guid? _activeId;
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
            // Один активный сбор за раз (полноценный single-active с проверкой БД — MLC-231; здесь
            // защищаемся in-memory стейтом, как PerfRecordingService).
            if (_activeId is { } current)
            {
                return new TechLogStartResult(TechLogStartOutcome.AlreadyActive, current);
            }

            var path = _store.ResolveLogcfgPath();
            if (path is null)
            {
                return new TechLogStartResult(
                    TechLogStartOutcome.RootNotFound, Guid.Empty,
                    Issue: "Не найден каталог conf платформы 1С (…\\1cv8\\conf). Проверьте установку 1С на узле.");
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

            _activeId = id;
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
            if (_activeId != collectionId)
            {
                return TechLogStopOutcome.NotActive;
            }

            // Восстановление исходного logcfg — главная гарантия (60_SAFETY №5): даже если БД-запись
            // ниже упадёт, конфиг уже снят. Идемпотентно: повторный restore без бэкапа — no-op.
            _store.RestoreOriginal();

            var now = _clock.GetUtcNow().UtcDateTime;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();

            var collection = await db.TechLogCollections
                .FirstOrDefaultAsync(c => c.Id == collectionId, ct)
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

            _activeId = null;
            _hasActive = false;
            LogRemoved(_logger, collectionId, reason);
            return TechLogStopOutcome.Stopped;
        }
        finally
        {
            _gate.Release();
        }
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
                // Активное дело есть — orphan-recovery (Active→Interrupted) и петля авто-стопа за MLC-231.
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
}
