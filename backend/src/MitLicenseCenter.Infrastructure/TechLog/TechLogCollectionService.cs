using System.Globalization;
using System.Text.Json;
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

    // Порог длительности для анализа долгих запросов в расследовании (5 000 000 µs = 5 c). Фильтр Dur в
    // logcfg для JSON-ТЖ 8.5 не работает (MLC-229) — порог применяет ISlowQueryAnalyzer. Рекомендация
    // интерфейса для расследований — 5 c (дефолт метода 1 c слишком шумный). Кладётся в снимок
    // CollectionConfig.DurationThresholdMicros (фактически применённый порог).
    private const long InvestigationSlowQueryThresholdMicros = 5_000_000;

    // Версия формы payload'а Finding.ResultJson (System.Text.Json-сериализация DTO анализатора).
    private const int FindingSchemaVersion = 1;

    private static readonly JsonSerializerOptions FindingJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogcfgBuilder _builder;
    private readonly ILogcfgStore _store;
    private readonly ITechLogParser _parser;
    private readonly ILockTreeAnalyzer _lockAnalyzer;
    private readonly ISlowQueryAnalyzer _slowQueryAnalyzer;
    private readonly IExceptionAnalyzer _exceptionAnalyzer;
    private readonly IDbmsLockAnalyzer _dbmsLockAnalyzer;
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
        ITechLogParser parser,
        ILockTreeAnalyzer lockAnalyzer,
        ISlowQueryAnalyzer slowQueryAnalyzer,
        IExceptionAnalyzer exceptionAnalyzer,
        IDbmsLockAnalyzer dbmsLockAnalyzer,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ILogger<TechLogCollectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _builder = builder;
        _store = store;
        _parser = parser;
        _lockAnalyzer = lockAnalyzer;
        _slowQueryAnalyzer = slowQueryAnalyzer;
        _exceptionAnalyzer = exceptionAnalyzer;
        _dbmsLockAnalyzer = dbmsLockAnalyzer;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public bool HasActiveCollection => _hasActive;

    public async Task<TechLogStartResult> InstallAsync(
        string startedBy, TechLogScenario scenario, string? infobaseProcessName, CancellationToken ct,
        Guid? infobaseId = null, Guid? tenantId = null)
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
                var existing = await checkDb.Investigations
                    .FirstOrDefaultAsync(c => c.Status == InvestigationStatus.Collecting, ct)
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

            // Бэкап исходного + запись целевого. Каталог сбора создаём (платформа пишет ТЖ под аккаунтом
            // агента 1С — см. проверку прав ниже).
            Directory.CreateDirectory(collectionDir);

            // Проба прав агента на каталог сбора (MLC-247 A2, 41_LOGCFG_SPEC §6, паттерн RAS-healing).
            // Процессы 1С пишут ТЖ под СВОИМ аккаунтом и должны иметь полные права на каталог; панель лишь
            // создаёт его. Аккаунт задан + прав нет → структурный отказ с точной командой icacls (сбор НЕ
            // стартует, чтобы не было «пустых дел»). Аккаунт пуст → НЕ блокируем (панель не знает аккаунт),
            // лишь предупреждение с шаблоном команды. «Проверка невозможна» (не-Windows/группы) — не блок.
            var agentAccount = _settings.GetString(SettingKey.TechLogCollectionAgentAccount);
            if (!string.IsNullOrWhiteSpace(agentAccount))
            {
                var aclProbe = _store.ProbeAgentDirectoryAccess(collectionDir, agentAccount);
                if (aclProbe is { Determined: true, HasAccess: false })
                {
                    return new TechLogStartResult(
                        TechLogStartOutcome.AgentNoCollectionAccess, Guid.Empty,
                        GrantCommand: aclProbe.GrantCommand, Issue: aclProbe.Issue);
                }
            }
            else
            {
                // Аккаунт не задан: панель не может проверить права — предупреждаем оператора шаблоном.
                LogAgentAccountUnset(_logger, collectionDir);
            }

            _store.WriteLogcfg(content);

            var id = Guid.NewGuid();
            var now = _clock.GetUtcNow().UtcDateTime;
            var collection = new Investigation
            {
                Id = id,
                Status = InvestigationStatus.Collecting,
                StartedAtUtc = now,
                // int-значения TechLogScenario и InvestigationScenario совпадают 1:1 (см. оба enum'а) —
                // прямой каст; Domain не зависит от Application, поэтому маппинг здесь, в адаптере.
                Scenario = (InvestigationScenario)(int)scenario,
                StartedBy = string.IsNullOrWhiteSpace(startedBy) ? "Unknown" : startedBy,
                // MLC-239: привязка к арендатору/инфобазе (резолв эндпоинтом из реестра). Закрывает разрыв
                // MLC-238: дело теперь несёт InfobaseId/TenantId, EnsureProcessFilterInvariant содержателен.
                InfobaseId = infobaseId,
                TenantId = tenantId,
                InfobaseProcessName = infobaseProcessName,
                CollectionDirectory = collectionDir,
                ConfigMarker = LogcfgBuilder.Marker,
                // Снимок включённого сбора (MLC-238, аудит/воспроизводимость, 50_DATA_MODEL §CollectionConfig).
                // Заполняется тем же, что реально попало в logcfg/применит анализатор:
                //   • Events — набор событий сценария из единого источника ILogcfgBuilder.EventsFor (CSV);
                //   • ProcessNameFilter — имя ИБ (p:processName), оно же InfobaseProcessName;
                //   • DurationThresholdMicros — фактический порог, который применит ISlowQueryAnalyzer
                //     (для сценариев без долгих запросов порог нерелевантен → null);
                //   • HistoryHours/LogcfgLocation — как в собранном logcfg; Format всегда "json" (8.5).
                CollectionConfig = new CollectionConfig
                {
                    LogcfgLocation = collectionDir,
                    Events = string.Join(',', _builder.EventsFor(scenario)),
                    DurationThresholdMicros = ScenarioUsesDurationThreshold(scenario)
                        ? InvestigationSlowQueryThresholdMicros
                        : null,
                    ProcessNameFilter = infobaseProcessName,
                    Format = "json",
                    HistoryHours = historyHours,
                },
            };

            // Инвариант изоляции арендатора (60_SAFETY №2): дело с InfobaseId обязано нести p:processName.
            // На уровне сервиса InfobaseId не приходит отдельно (приходит infobaseProcessName), но контракт
            // домена держим явно — снимок наполнен, проверяем сразу после установки (как требует 50_DATA_MODEL).
            collection.EnsureProcessFilterInvariant();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            db.Investigations.Add(collection);

            var initiator = string.IsNullOrWhiteSpace(startedBy) ? "Unknown" : startedBy;
            var scopeText = infobaseProcessName is null ? "весь кластер" : $"ИБ {infobaseProcessName}";
            audit.Enlist(
                AuditActionType.TechLogCollectionStarted,
                initiator,
                $"Запущен сбор технологического журнала: сценарий {scenario}, {scopeText}.");
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            _active = new ActiveState(id, now, collectionDir, collection.Scenario);
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
        Guid collectionId, InvestigationStopReason reason, CancellationToken ct)
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
                await FinalizeActiveLockedAsync(InvestigationStopReason.TimeLimit, ct).ConfigureAwait(false);
                return;
            }

            // Авто-стоп по лимиту места (60_SAFETY №3): размер каталога сбора ≥ TechLog.DiskLimitMb.
            // Размер — за seam'ом store (тест симулирует превышение без файлов).
            var diskLimitMb = ResolveSetting(SettingKey.TechLogDiskLimitMb, DefaultDiskLimitMb);
            var sizeBytes = _store.GetDirectorySizeBytes(active.CollectionDirectory);
            if (sizeBytes >= (long)diskLimitMb * BytesPerMb)
            {
                await FinalizeActiveLockedAsync(InvestigationStopReason.DiskLimit, ct).ConfigureAwait(false);
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

            var orphaned = await db.Investigations
                .Where(c => c.Status == InvestigationStatus.Collecting)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (orphaned.Count == 0)
            {
                return;
            }

            foreach (var collection in orphaned)
            {
                collection.Status = InvestigationStatus.Interrupted;
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

    // Закрывает активное дело (вызывается под _gate) и проводит его через конвейер анализа (MLC-238):
    //   1) Восстановление исходного logcfg — ГЛАВНАЯ гарантия безопасности (60_SAFETY №5): делаем ДО
    //      анализа, даже если дальше всё упадёт — сбор уже снят.
    //   2) Сбор снят → аудит 807 (как было); дело Collecting → Analyzing (targeted-UPDATE).
    //   3) Чтение сырья ТЖ из каталога (seam ILogcfgStore.ReadCollectionLines) → парсер → анализаторы
    //      по сценарию дела → Finding'и (версионированный JSON). Анализаторы/парсер never-throws, но
    //      чтение диска/сериализация могут бросить — оборачиваем.
    //   4) Успех → Completed + StopReason, сырьё ТЖ удаляем (решение MLC-237 Q2). Ошибка конвейера →
    //      Failed (структурный лог, НЕ новый audit-код), сырьё НЕ удаляем (нужно для разбора).
    // Аудит снятия (807) уже покрывает «сбор снят»; внутренние переходы Collecting→Analyzing→Completed
    // отдельного audit-действия не требуют (BE-only, без FE-parity новых кодов).
    private async Task FinalizeActiveLockedAsync(InvestigationStopReason reason, CancellationToken ct)
    {
        var active = _active!;

        // (1) Восстановление конфига — раньше всего (главная гарантия). Дальше дело уже не активно.
        _store.RestoreOriginal();
        _active = null;
        _hasActive = false;

        // (2) Снятие сбора → Analyzing + аудит 807. Отдельный scope/SaveChanges, чтобы переход состояния
        //     был зафиксирован до (возможно длительного) разбора.
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            var collection = await db.Investigations
                .FirstOrDefaultAsync(c => c.Id == active.Id, ct)
                .ConfigureAwait(false);
            if (collection is null)
            {
                // Дела нет в БД (удалено/перенесено) — нечего анализировать. Снятие уже выполнено.
                LogRemoved(_logger, active.Id, reason);
                return;
            }

            collection.Status = InvestigationStatus.Analyzing;
            audit.Enlist(
                AuditActionType.TechLogCollectionStopped,
                SystemInitiator,
                $"Сбор технологического журнала остановлен ({reason}).");
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        LogRemoved(_logger, active.Id, reason);

        // (3)+(4) Конвейер анализа — never валит хост-сервис/сторож: любую ошибку переводим дело в Failed.
        await RunAnalysisPipelineAsync(active.Id, active.CollectionDirectory, active.Scenario, reason, ct)
            .ConfigureAwait(false);
    }

    // Конвейер анализа снятого дела (MLC-238): сырьё → парсер → анализаторы сценария → Finding'и →
    // Completed + удаление сырья. Любая ошибка (чтение диска/сериализация/БД) → Failed, сырьё сохраняем.
    private async Task RunAnalysisPipelineAsync(
        Guid investigationId,
        string collectionDirectory,
        InvestigationScenario scenario,
        InvestigationStopReason reason,
        CancellationToken ct)
    {
        try
        {
            // Сырьё ТЖ за seam'ом store; парсер never-throws. Материализуем события в список: GeneralSlow
            // прогоняет ДВА анализатора (каждый перечисляет поток), а ParseReader/ParseLines — single-pass
            // ленивые. Разбор — разовый при снятии, объём режут event-scope фильтры logcfg, материализация
            // приемлема.
            var parseResult = _parser.ParseLines(_store.ReadCollectionLines(collectionDirectory));
            var events = parseResult.Events.ToList();

            var findings = BuildFindings(investigationId, scenario, events);

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var collection = await db.Investigations
                    .FirstOrDefaultAsync(c => c.Id == investigationId, ct)
                    .ConfigureAwait(false);
                if (collection is null)
                {
                    return; // дело исчезло между переходами — нечего завершать
                }

                foreach (var finding in findings)
                {
                    db.Findings.Add(finding);
                }

                collection.Status = InvestigationStatus.Completed;
                collection.StopReason = reason;
                collection.StoppedAtUtc = _clock.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            // Сырьё ТЖ удаляем только после успешного наполнения Finding'ов (решение MLC-237 Q2):
            // разобранный результат уже в БД, сырое держать незачем (объём, ПДн арендаторов).
            _store.DeleteCollectionFiles(collectionDirectory);
            LogAnalyzed(_logger, investigationId, scenario, findings.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Ошибка конвейера (чтение диска/сериализация/БД) — дело Failed, сырьё НЕ удаляем (нужно для
            // разбора). Failed — структурный лог, НЕ новый audit-код (снятие уже аудировано 807).
            await MarkFailedAsync(investigationId, ct).ConfigureAwait(false);
            LogAnalysisFailed(_logger, investigationId, ex);
        }
    }

    // Прогон анализаторов этапа B по сценарию дела → Finding'и (по одному на результат анализатора).
    // GeneralSlow — комбинированный сценарий (CALL/DBMSSQL): разумный набор = долгие запросы + исключения
    // (две находки), даёт картину «что медленно и что падает» без перегрузки. ResultJson = System.Text.Json
    // сериализация DTO анализатора; SchemaVersion=1; Kind соответствует анализатору. Привязку к арендатору
    // (нормализация p:processName) анализаторы делают сами внутри DTO — здесь не дублируем.
    private List<Finding> BuildFindings(
        Guid investigationId, InvestigationScenario scenario, IReadOnlyList<TechLogEvent> events)
    {
        var findings = new List<Finding>();

        switch (scenario)
        {
            case InvestigationScenario.Locks:
                findings.Add(NewFinding(investigationId, FindingKind.ManagedLocks, _lockAnalyzer.Analyze(events)));
                break;
            case InvestigationScenario.SlowQueries:
                findings.Add(NewFinding(investigationId, FindingKind.SlowQueries,
                    _slowQueryAnalyzer.Analyze(events, InvestigationSlowQueryThresholdMicros)));
                break;
            case InvestigationScenario.Exceptions:
                findings.Add(NewFinding(investigationId, FindingKind.Exceptions, _exceptionAnalyzer.Analyze(events)));
                break;
            case InvestigationScenario.DbmsLocks:
                findings.Add(NewFinding(investigationId, FindingKind.DbmsLocks, _dbmsLockAnalyzer.Analyze(events)));
                break;
            case InvestigationScenario.GeneralSlow:
                // Комбинированный: долгие запросы + исключения (решение MLC-238).
                findings.Add(NewFinding(investigationId, FindingKind.SlowQueries,
                    _slowQueryAnalyzer.Analyze(events, InvestigationSlowQueryThresholdMicros)));
                findings.Add(NewFinding(investigationId, FindingKind.Exceptions, _exceptionAnalyzer.Analyze(events)));
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario), scenario, "Неизвестный сценарий расследования для конвейера анализа.");
        }

        return findings;
    }

    private static Finding NewFinding<TResult>(Guid investigationId, FindingKind kind, TResult result)
        => new()
        {
            Id = Guid.NewGuid(),
            InvestigationId = investigationId,
            Kind = kind,
            SchemaVersion = FindingSchemaVersion,
            ResultJson = JsonSerializer.Serialize(result, FindingJsonOptions),
        };

    // Перевод дела в Failed (отдельный scope/SaveChanges; не бросает — best-effort при провале конвейера).
    private async Task MarkFailedAsync(Guid investigationId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var collection = await db.Investigations
                .FirstOrDefaultAsync(c => c.Id == investigationId, ct)
                .ConfigureAwait(false);
            if (collection is null)
            {
                return;
            }

            collection.Status = InvestigationStatus.Failed;
            collection.StoppedAtUtc = _clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Не удалось даже зафиксировать Failed — логируем, не валим сторож.
            LogAnalysisFailed(_logger, investigationId, ex);
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

            var hasActive = await db.Investigations
                .AnyAsync(c => c.Status == InvestigationStatus.Collecting, ct)
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

    // Снимок активного дела в памяти (id + старт для окна времени + каталог для лимита места + сценарий
    // для выбора анализаторов в конвейере MLC-238).
    private sealed class ActiveState(Guid id, DateTime startedAtUtc, string collectionDirectory, InvestigationScenario scenario)
    {
        public Guid Id { get; } = id;
        public DateTime StartedAtUtc { get; } = startedAtUtc;
        public string CollectionDirectory { get; } = collectionDirectory;
        public InvestigationScenario Scenario { get; } = scenario;
    }

    // Релевантен ли порог длительности сценарию (для снимка CollectionConfig.DurationThresholdMicros).
    // Долгие запросы напрямую и GeneralSlow (в нём — анализ долгих запросов) применяют порог; прочие
    // сценарии порогом не отбирают → null (порога нет).
    private static bool ScenarioUsesDurationThreshold(TechLogScenario scenario)
        => scenario is TechLogScenario.SlowQueries or TechLogScenario.GeneralSlow;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Tech-log collection {CollectionId} installed (scenario {Scenario}, infobase {Infobase})")]
    private static partial void LogInstalled(ILogger logger, Guid collectionId, TechLogScenario scenario, string infobase);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Tech-log collection {CollectionId} removed ({Reason})")]
    private static partial void LogRemoved(ILogger logger, Guid collectionId, InvestigationStopReason reason);

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

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Investigation {InvestigationId} analyzed (scenario {Scenario}, {FindingCount} finding(s)) — Completed")]
    private static partial void LogAnalyzed(ILogger logger, Guid investigationId, InvestigationScenario scenario, int findingCount);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Investigation {InvestigationId} analysis pipeline failed — marked Failed (raw tech-log kept)")]
    private static partial void LogAnalysisFailed(ILogger logger, Guid investigationId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Tech-log: agent account (TechLog.CollectionAgentAccount) is not set — cannot verify write access on the collection directory {Directory}. If the platform writes nothing, grant the 1C agent account Modify: icacls \"{Directory}\" /grant \"<account>:(OI)(CI)(M)\" /T")]
    private static partial void LogAgentAccountUnset(ILogger logger, string directory);
}
