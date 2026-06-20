using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Application.Identity;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Maintenance;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Application.Reporting;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Application.Updates;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Backups;
using MitLicenseCenter.Infrastructure.Clusters;
using MitLicenseCenter.Infrastructure.Diagnostics;
using MitLicenseCenter.Infrastructure.Discovery;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Maintenance;
using MitLicenseCenter.Infrastructure.Performance;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Publishing;
using MitLicenseCenter.Infrastructure.Ras;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Infrastructure.Server;
using MitLicenseCenter.Infrastructure.Settings;
using MitLicenseCenter.Infrastructure.TechLog;
using MitLicenseCenter.Infrastructure.Updates;

namespace MitLicenseCenter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Не задана строка подключения 'ConnectionStrings:Default'. " +
                "Укажите её в appsettings.{Environment}.json, User Secrets или переменной окружения.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
                sql.EnableRetryOnFailure(maxRetryCount: 3);
            });

            // MLC-038 (PERF-02): опт-ин профиль EF-команд для замера baseline-запросов.
            // По умолчанию выключен — навешивается ТОЛЬКО когда задан флаг
            // Diagnostics:EfQueryProfiling (config/env). Свой приёмник LogTo не зависит от
            // секции Logging в appsettings, поэтому прод-уровень (Database.Command=Warning)
            // и прод-поведение при выключенном флаге 1:1. Фильтр по CommandExecuted даёт
            // ровно «Executed DbCommand (Xms) … SQL» без шума open/close-соединений.
            if (EfQueryProfiling.IsEnabled(configuration))
            {
                options.LogTo(
                    EfQueryProfiling.BuildSink(configuration),
                    new[] { RelationalEventId.CommandExecuted },
                    LogLevel.Information);

                // Значения параметров в открытом виде — только при отдельном явном opt-in
                // (gated в IsSensitiveEnabled). Никогда не включается без флага.
                if (EfQueryProfiling.IsSensitiveEnabled(configuration))
                {
                    options.EnableSensitiveDataLogging();
                }
            }
        });

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 12;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // MLC-109 (SEC-01) — немедленный отзыв доступа при disable / reset-password / смене роли.
        // Cookie-кука sliding 8h: при активности жертвы доступ был фактически бессрочным, т.к.
        // отзыв роли/пароля/учётки не убивал уже выданную куку. Лечится security-stamp'ом: при
        // каждой такой операции мы ротируем AspNetUsers.SecurityStamp (UpdateSecurityStampAsync),
        // а SecurityStampValidator на каждом OnValidatePrincipal сверяет stamp из куки с БД —
        // расхождение → кука отвергается (SignOut). Гоча: AddIdentityCore (в отличие от полного
        // AddIdentity) НЕ регистрирует ни ISecurityStampValidator, ни ITwoFactorSecurityStampValidator
        // и не настраивает SecurityStampValidatorOptions — поэтому регистрируем их явно здесь, рядом
        // с самой регистрацией Identity (cookie-пайплайн в Web подключает валидатор к
        // OnValidatePrincipal). Без ITwoFactorSecurityStampValidator SecurityStampValidator падает
        // на резолве зависимости, хотя 2FA в панели не используется.
        services.AddScoped<ISecurityStampValidator, SecurityStampValidator<AppUser>>();
        services.AddScoped<ITwoFactorSecurityStampValidator, TwoFactorSecurityStampValidator<AppUser>>();

        // Интервал ревалидации куки. Компромисс «свежесть отзыва ↔ нагрузка на БД»: при каждом
        // запросе с уже провалидированной кукой stamp НЕ перечитывается — только раз в интервал.
        // 2 минуты — верхняя граница задержки, с которой отозванная кука перестаёт работать
        // (требование SEC-01: 1–5 мин). Меньше — лишние round-trip'ы в БД на горячем пути; больше —
        // окно, в которое уволенный/разжалованный ещё ходит со старой кукой.
        services.Configure<SecurityStampValidatorOptions>(o =>
        {
            o.ValidationInterval = TimeSpan.FromMinutes(2);
        });

        AddDataProtection(services, environment);

        services.TryAddSingletonTimeProvider();
        services.AddScoped<IAuditLogger, AuditLogger>();

        // MLC-058 — генератор временного пароля для администраторских операций веб-панели
        // (создание / сброс пароля). Обёртка над единым генератором сидера: парити с
        // парольной политикой Identity без второго источника. Stateless → singleton.
        services.AddSingleton<IInitialPasswordGenerator, InitialPasswordGenerator>();

        // Settings: singleton snapshot (in-mem TTL ≈ 30s) + scoped store
        // (DbContext-bound). Mutate через store → store.Invalidate() сбрасывает snapshot.
        services.AddSingleton<ISettingsSnapshot, SettingsSnapshot>();
        services.AddScoped<ISettingsStore, SettingsStore>();

        // MLC-176: проверка обновлений через GitHub Releases — первый исходящий HTTP в
        // проекте. Типизированный HttpClient к публичному GitHub API. User-Agent
        // обязателен (без него GitHub отвечает 403); Accept + X-GitHub-Api-Version
        // фиксируют контракт v3 (2022-11-28). Короткий таймаут 10с: проверка фоновая и
        // не должна вешать запрос /updates/status. Реализация ловит все сбои → null.
        services.AddHttpClient<IGitHubReleaseClient, GitHubReleaseClient>(c =>
        {
            c.BaseAddress = new Uri("https://api.github.com/");
            c.Timeout = TimeSpan.FromSeconds(10);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MitLicenseCenter");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            c.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        });

        // Cluster adapter: rac.exe wrapper — единственный 1С cluster-адаптер
        // (Stage 5 PR 5.1, ADR-16). REST adapter и Polly circuit breaker удалены —
        // они хеджировали primary, эмпирически отсутствующий на default-деплоях 1С 8.5.
        // CLI-контракт зафиксирован в ADR-3.3.
        services.AddSingleton<IRacProcessRunner, SystemProcessRacRunner>();
        // Кросс-вызовный кэш UUID кластера (MLC-041): singleton, переживает scope'ы
        // hot/cold-джобов — снимает лишний «cluster list» перед каждой командой.
        services.AddSingleton<IClusterUuidCache, ClusterUuidCache>();
        services.AddScoped<IClusterClient, RacExecutableRasClusterClient>();

        // RAS health probing: независимый 30s ping-loop публикует IRasHealthReader
        // snapshot для Dashboard. Аудит-нейтрален в PR 5.1 (см. plan A6).
        services.AddSingleton<RasHealthState>();
        services.AddSingleton<IRasHealthReader>(sp => sp.GetRequiredService<RasHealthState>());
        services.AddSingleton<RasHealthProbingService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<RasHealthProbingService>());

        // Управление службой RAS (MLC-159, ADR-47; обнаружение — MLC-162): служба
        // ищется по ImagePath с ras.exe через реестр (IServiceRegistryReader, один
        // проход без спавнов), состояние — через ServiceController (IServiceStateReader);
        // register/update/start — через sc.exe. Ортогонально rac.exe-адаптеру (протокол
        // RAS не трогаем). sc-раннер декодирует OEM-вывод как rac/iisreset; резолвер
        // ras.exe — по версионным bin-каталогам 1С (как rac.exe). Все singleton (без
        // состояния, читают реестр/ServiceController/ISettingsSnapshot/ФС). Windows-only,
        // но реестр/спавн — с platform-guard, без #pragma CA1416.
        services.AddSingleton<IScProcessRunner, ScProcessRunner>();
        services.AddSingleton<IServiceRegistryReader, RegistryServiceReader>();
        services.AddSingleton<IServiceStateReader, ServiceControllerStateReader>();
        services.AddSingleton<IRasExePathResolver, RasExePathResolver>();
        services.AddSingleton<IRasServiceManager, ScRasServiceManager>();

        // Универсальный надёжный контроллер службы Windows (MLC-212, ADR-55): команда
        // (sc start/sc stop) + верификация фактического состояния опросом ServiceController
        // до целевого с таймаутом; идемпотентность (sc 1056/1062 = успех). Singleton:
        // контроллер без состояния (переиспользует IScProcessRunner/IServiceStateReader),
        // gate держит словарь семафоров per-service-name на весь процесс. Дефолтные
        // таймауты задаются опциями (тесты подменяют через ctor): VerificationTimeout 300с
        // (MLC-224 — холодный старт/стоп кластера 1С идёт минутами; полл возвращается сразу
        // при достижении состояния, потолок бьёт лишь по детекту реального отказа). Эндпоинты/FE — MLC-213+.
        services.AddSingleton<IServiceOperationGate, ServiceOperationGate>();
        services.AddSingleton(new WindowsServiceControllerOptions());
        services.AddSingleton<IWindowsServiceController, WindowsServiceController>();

        // Рестарт рабочего процесса 1С (rphost) по Pid (MLC-220, ADR-56). У rac нет команды
        // «restart process» → рестарт = завершение ОС-процесса rphost (ILocalProcessTerminator,
        // тонкая граница над System.Diagnostics.Process — ADR-20), кластер авто-поднимает новый.
        // Сервис: whitelist по rac process list (IClusterClient) → guard по имени процесса →
        // kill → верификация исчезновения Pid опросом rac с таймаутом (TimeProvider). Terminator —
        // singleton без состояния; сам сервис Scoped (зависит от scoped IClusterClient). Дефолтные
        // таймауты (30с) задаются опциями; тесты подменяют через ctor. Эндпоинт — ServerEndpoints; FE — MLC-220.
        services.AddSingleton<ILocalProcessTerminator, LocalProcessTerminator>();
        services.AddSingleton(new OneCProcessRestartOptions());
        services.AddScoped<IOneCProcessRestartService, OneCProcessRestartService>();

        // Read-агрегатор статуса служб узла + управление сервером 1С (MLC-213, ADR-54/55):
        // обнаружение служб ragent через реестр (IServiceRegistryReader, один проход без
        // спавнов — как RAS) + состояние (IServiceStateReader); статус службы SQL —
        // sqlservr discovery + имя инстанса (ISqlInstanceDiscovery), never-throws; провайдер
        // композирует обнаружение ragent + IRasServiceManager + статус SQL +
        // IIisLifecycleService, каждый источник деградирует независимо (Available/Error).
        // Только наблюдение для RAS/SQL/IIS; мутации сервера 1С — через IWindowsServiceController
        // (выше). Источники-ридеры — singleton (без состояния, читают реестр/ServiceController).
        // Сам ПРОВАЙДЕР — Scoped (MLC-222): он композирует Scoped `IIisLifecycleService` (ниже),
        // а singleton НЕ может потреблять scoped — DI ValidateScopes валит старт в Development
        // (captive dependency). Потребляется только per-request эндпоинтами ServerEndpoints, так
        // что Scoped корректен. Эндпоинты — ServerEndpoints; FE — MLC-214.
        services.AddSingleton<IOneCServerDiscovery, OneCServerDiscovery>();
        services.AddSingleton<ISqlServiceStatusReader, SqlServiceStatusReader>();
        services.AddScoped<IServerStatusProvider, ServerStatusProvider>();

        // IIS publishing: реальный адаптер ServerManager + XDocument (PR 3.5).
        // Stub переехал в Publishing/Testing/ для unit-тестов, в production-DI
        // не регистрируется — реальный OneCIisPublishingService требует Windows.
#pragma warning disable CA1416 // Validate platform compatibility — single-node deployment is Windows-only by design (memory/infrastructure_integration.md).
        services.AddScoped<IIisPublishingService, OneCIisPublishingService>();

        // IIS lifecycle (MLC-047, ADR-24): recycle/start/stop пула, start/stop/restart
        // сайта, iisreset. ServerManager + спавн iisreset.exe — тоже Windows-only.
        services.AddScoped<IIisLifecycleService, OneCIisLifecycleService>();
#pragma warning restore CA1416

        // MLC-047: сериализатор разрушительных IIS-операций (N=1). Singleton — кэп общий
        // на весь процесс (single-node): два recycle/iisreset одновременно недопустимы.
        services.AddSingleton<IIisResetConcurrencyGate, IisResetConcurrencyGate>();

        // Host-метрики раздела «Быстродействие» (MLC-064, ADR-26): WMI + Process, Windows-only.
        // Singleton — держит предыдущий снимок CPU-времён процессов и сырые perf-счётчики диска
        // для дельты между poll'ами (паттерн ColdThrottleState/IClusterUuidCache); первый poll
        // отдаёт Measuring=true. В тестах — StubHostMetricsProbe (реальный требует Windows).
#pragma warning disable CA1416 // Validate platform compatibility — single-node deployment is Windows-only by design.
        services.AddSingleton<IHostMetricsProbe, OneCHostMetricsProbe>();
#pragma warning restore CA1416

        // SQL DMV-проба раздела «Быстродействие» (MLC-068, ADR-26, Фаза 3): активные запросы /
        // блокировки / IO-stall / дельта wait-stats. Чистый ADO.NET (как SqlDatabaseDiscovery) —
        // НЕ Windows-only, без #pragma CA1416. Singleton — держит предыдущий срез wait/IO для
        // дельты между poll'ами (первый poll → Measuring=true). Строку наследует из
        // ConnectionStrings:Default; в тестах — StubSqlPerformanceProbe (реальная ходит в SQL).
        services.AddSingleton<ISqlPerformanceProbe, SqlPerformanceProbe>();

        // Запись по требованию раздела «Быстродействие» (MLC-070, ADR-26, Фаза 4). Singleton —
        // держит активную запись + счётчик сэмплов между тиками (как ColdThrottleState/аккумулятор
        // лицензий); БД и scoped IClusterClient берёт через IServiceScopeFactory. Фоновый драйвер
        // PerfRecordingSamplingService тикает по таймеру и зовёт SampleOnceAsync (паттерн
        // HotTierPollingService — sub-minute таймер вне Hangfire CRON).
        services.AddSingleton<IPerfRecordingService, PerfRecordingService>();
        services.AddSingleton<PerfRecordingSamplingService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<PerfRecordingSamplingService>());

        // Сбор технологического журнала режима «Расследование» (MLC-230/231, ADR-57/58). Генератор
        // logcfg (ILogcfgBuilder) и store (ILogcfgStore) — stateless singleton'ы (чистый XML / ФС
        // за интерфейсом). Сервис жизненного цикла (ITechLogCollectionService) — singleton: держит
        // активное дело между операциями и сериализует их через SemaphoreSlim (паттерн
        // PerfRecordingService); БД и scoped IAuditLogger берёт через IServiceScopeFactory. Драйвер
        // безопасного сбора (TechLogWatchdogService) — hosted BackgroundService: на старте
        // orphan-recovery (Active→Interrupted) → стартовая сверка файла (снимает «забытый» конфиг),
        // затем периодический сторож активного дела — авто-стоп по окну времени (TimeLimit) и лимиту
        // места (DiskLimit). Single-active по БД и сторож места перед стартом — в InstallAsync
        // (60_SAFETY №3/№4/№5).
        services.AddSingleton<ILogcfgBuilder, LogcfgBuilder>();
        // Парсер NDJSON-ТЖ (MLC-232, ядро этапа B) — stateless singleton, чистый C# без ФС, как
        // LogcfgBuilder. Поверх него встанут анализаторы блокировок/долгих запросов/исключений.
        services.AddSingleton<ITechLogParser, TechLogParser>();
        // Анализатор управляемых блокировок 1С (MLC-233, этап B) — ТОЛЬКО 1С-уровень
        // (TLOCK/TTIMEOUT/TDEADLOCK); СУБД-уровень (<dbmslocks/>/lkX) — MLC-236.
        // Stateless singleton, чистый C# без ФС/БД (как TechLogParser/LogcfgBuilder).
        services.AddSingleton<ILockTreeAnalyzer, LockTreeAnalyzer>();
        // Анализатор долгих запросов к СУБД (MLC-234, этап B) — ТОЛЬКО DBMSSQL.
        // Строит топ долгих + группы похожих (нормализация SQL). Порог длительности —
        // в анализаторе, не в logcfg (фильтр Dur в JSON-ТЖ 8.5 не работает, §6).
        // Привязка к ИБ через TechLogProcessName.Normalize (общий хелпер MLC-234).
        // Stateless singleton, чистый C# без ФС/БД (как LockTreeAnalyzer).
        services.AddSingleton<ISlowQueryAnalyzer, SlowQueryAnalyzer>();
        services.AddSingleton<ILogcfgStore, LogcfgStore>();
        services.AddSingleton<ITechLogCollectionService, TechLogCollectionService>();
        services.AddSingleton<TechLogWatchdogService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<TechLogWatchdogService>());

        // On-demand бэкап баз SQL (MLC-076, ADR-27): весь безопасный цикл одной операции
        // (sysadmin-проверка → оценка → место → BACKUP COPY_ONLY → VERIFYONLY → keep-latest).
        // Чистый ADO.NET (как SqlPerformanceProbe) — НЕ Windows-only, без #pragma CA1416.
        // Stateless → singleton; строку наследует из ConnectionStrings:Default. В тестах —
        // FakeSqlBackupService (реальный адаптер ходит в SQL, integration-only).
        services.AddSingleton<ISqlBackupService, SqlBackupAdapter>();

        // Замер размеров баз SQL (MLC-185): один запрос к sys.master_files — выделенное
        // место (данные/лог) всех пользовательских баз инстанса. Чистый ADO.NET (как
        // SqlBackupAdapter/SqlPerformanceProbe) — НЕ Windows-only, без #pragma CA1416.
        // Stateless → singleton; сервер из настройки Sql.Server, остальное наследует из
        // ConnectionStrings:Default. В тестах — FakeDatabaseSizeProbe (реальный адаптер
        // ходит в SQL, integration-only).
        services.AddSingleton<IDatabaseSizeProbe, DatabaseSizeProbe>();

        // Проба обслуживания SQL раздела «Сервер» → вкладка «Обслуживание» (MLC-216, ADR-54):
        // live-read свежести резервных копий баз из msdb.dbo.backupset (БЕЗ собственных
        // таблиц/миграций/джоб). Чистый ADO.NET (как DatabaseSizeProbe/SqlBackupAdapter) — НЕ
        // Windows-only, без #pragma CA1416. Stateless → singleton; сервер из настройки
        // Sql.Server, остальное наследует из ConnectionStrings:Default. Never-throws: нет прав
        // на backupset → PermissionDenied, нет SQL → Unavailable. В тестах — ручной фейк
        // (реальный адаптер ходит в SQL). MLC-217 дорастит ЭТУ же пробу планами обслуживания.
        services.AddSingleton<IMaintenanceProbe, SqlMaintenanceProbe>();

        // Оркестратор очереди бэкапов (MLC-077, ADR-27). Singleton — держит in-memory набор
        // выполняющихся пар server+db (замок-на-базу) и wake-сигнал насоса между запросами;
        // БД и scoped IAuditLogger берёт через IServiceScopeFactory (паттерн
        // PerfRecordingService). Фоновый BackupPumpService тикает по wake-или-таймауту и
        // зовёт PumpOnceAsync; на старте закрывает осиротевшие Running как Interrupted.
        services.AddSingleton<IBackupOrchestrator, BackupOrchestrator>();
        services.AddSingleton<BackupPumpService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<BackupPumpService>());

        // Публикация через webinst.exe (MLC-045, ADR-20). Scoped — читает ISettingsSnapshot;
        // запускает процесс webinst версии платформы из публикации.
        services.AddScoped<IWebinstPublisher, OneCWebinstPublisher>();

        // MLC-046: ограничитель одновременных спавнов webinst (массовая публикация = N
        // одиночных вызовов). Singleton — кэп общий на весь процесс (single-node), защита
        // спавн-бюджета независимо от клиента (семья ADR-3.3).
        services.AddSingleton<IWebinstConcurrencyGate, WebinstConcurrencyGate>();

        // Discovery (интерактивная настройка форм): SQL-перечисление БД и скан rac.exe.
        services.AddScoped<ISqlDatabaseDiscovery, SqlDatabaseDiscovery>();
        services.AddSingleton<IRacPathDiscovery, RacPathDiscovery>();
        services.AddSingleton<IPlatformVersionDiscovery, PlatformVersionDiscovery>();
        // MLC-056: инстансы SQL из локального реестра. Singleton — машинно-локальный
        // снимок (как rac/platform), не per-request в отличие от SqlDatabaseDiscovery.
        services.AddSingleton<ISqlInstanceDiscovery, SqlInstanceDiscovery>();

        // Snapshot store + hot-tier registry (singletons, PR 3.3).
        services.AddSingleton<IActiveSessionSnapshotStore, ActiveSessionSnapshotStore>();
        services.AddSingleton<IHotTierRegistry, HotTierRegistry>();

        // License fact cache (ADR-48, MLC-166): singleton — холодный цикл пишет факт
        // `rac --licenses`, горячий тир читает классификацию без второго спавна rac.exe.
        services.AddSingleton<ILicenseFactCache, LicenseFactCache>();

        // License usage accumulator (MLC-048, ADR-25): singleton — состояние текущего
        // 15-мин бакета переживает scoped-инвокации cold-цикла.
        services.AddSingleton<ILicenseUsageAccumulator, LicenseUsageAccumulator>();

        // MLC-037 (PERF-01): метрики горячего пути (Meter'ы спавнов rac.exe и цикла
        // согласования). Singleton'ы — Meter живёт на весь процесс; снимаются через
        // dotnet-counters (см. OPERATIONS.md «Наблюдаемость перфа»). IMeterFactory
        // предоставляется хостом (AddMetrics в generic host).
        services.AddSingleton<RacMetrics>();
        services.AddSingleton<ReconciliationMetrics>();

        // MLC-044: общий enforcement-замок. Singleton — берут оба пути enforcement
        // (cold Hangfire-джоб и hot BackgroundService), чтобы kill исполнял ровно один
        // путь за раз (защита от over-kill, MLC-001; Hangfire-фильтр hot не покрывает).
        services.AddSingleton<IEnforcementGate, EnforcementGate>();

        // Reconciliation job + kill enforcer (scoped — require DbContext + IClusterClient).
        services.AddScoped<IReconciliationJob, ReconciliationJob>();
        services.AddScoped<IKillEnforcer, KillEnforcer>();

        // Publication status refresh job (MLC-045): read-only обновление статуса
        // публикаций в IIS (без enforcement/аудита). Scoped (зависит от DbContext),
        // плюс singleton throttle-state по аналогии с ColdThrottleState.
        services.AddSingleton<StatusRefreshThrottleState>();
        services.AddScoped<IPublicationStatusJob, PublicationStatusRefreshJob>();

        // Audit retention (PR 4.3): scoped (DbContext + IAuditLogger), без
        // throttle-state — CRON фиксирован 03:00 daily, не tuneable оператором.
        services.AddScoped<IAuditRetentionJob, AuditRetentionJob>();

        // License usage retention (MLC-048): scoped (DbContext), CRON фиксирован 03:30 daily.
        services.AddScoped<ILicenseUsageRetentionJob, LicenseUsageRetentionJob>();

        // Database size collection + retention (MLC-185c): scoped (DbContext). Сбор — 02:00,
        // ретеншен — 04:00 daily; CRON'ы фиксированы в Program.cs. Retention window —
        // Settings.DatabaseSize.RetentionDays.
        services.AddScoped<IDatabaseSizeCollectionJob, DatabaseSizeCollectionJob>();
        services.AddScoped<IDatabaseSizeRetentionJob, DatabaseSizeRetentionJob>();

        // Perf recording retention (MLC-169): scoped (DbContext), CRON фиксирован 03:45 daily.
        // Срок хранения — константа в джобе (не Setting), окно оператором не настраивается.
        services.AddScoped<IPerfRecordingRetentionJob, PerfRecordingRetentionJob>();

        // Backup retention (MLC-077, ADR-27): scoped (DbContext + IAuditLogger), CRON
        // фиксирован 03:15 daily; TTL настраивается через Settings.Backup.TtlHours.
        services.AddScoped<IBackupRetentionJob, BackupRetentionJob>();

        // Авто-рестарт сервера 1С (MLC-218, ADR-55): scoped (ISettingsStore=DbContext +
        // IAuditLogger). CRON — НЕ фиксирован: строится из OneC.AutoRestart.Time, регистрация/
        // снятие из Program.cs (старт) и эндпоинта /server/auto-restart. Тело рестартит
        // запущенные ragent через IWindowsServiceController (singleton, выше).
        services.AddScoped<IOneCAutoRestartJob, OneCAutoRestartJob>();

        // Hot-tier polling: BackgroundService для sub-minute hot-poll (Hangfire
        // CRON minimum = 1 мин, а нам нужно 3–5s). См. ADR-6.1.
        services.AddSingleton<HotTierPollingService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<HotTierPollingService>());

        // Cold-tier polling (MLC-154): BackgroundService для cold-обхода сессий. Раньше
        // был рекуррентным Hangfire-джобом с CRON "* * * * *", но Hangfire-CRON minimum =
        // 1 мин делал настройку Polling.ColdIntervalSeconds (10–300с) инертной. Таймер
        // сервиса читает интервал каждый цикл → каданс реален. Лок Hangfire не нужен:
        // single-host (ADR-28) + петля последовательна, cold↔hot сериализует IEnforcementGate.
        // См. ADR-6.1.
        services.AddSingleton<ColdTierPollingService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ColdTierPollingService>());
        // MLC-156: тот же singleton за портом ISessionRefreshTrigger — эндпоинт
        // POST /sessions/refresh форсит немедленный cold-прогон, не ломая границу слоёв.
        services.AddSingleton<ISessionRefreshTrigger>(sp => sp.GetRequiredService<ColdTierPollingService>());

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(TimeProvider)))
        {
            return;
        }

        services.AddSingleton(TimeProvider.System);
    }

    private static void AddDataProtection(IServiceCollection services, IHostEnvironment environment)
    {
        // MLC-189: интеграционный тест-хост (env "Test", MlcWebApplicationFactory) НЕ должен
        // зависеть от записи в %ProgramData%\MitLicenseCenter\keys. Боевой key ring защищён
        // NTFS ACL (установщик раздаёт доступ только сервис-аккаунту/админам — ADR-8); в
        // worktree/CI у тест-процесса этих прав нет → `Directory.CreateDirectory`/запись ключа
        // падали уже при подъёме хоста, валя 8 middleware-тестов (SecurityHeaders/LoginRateLimit).
        // Уводим key ring в %TEMP% (всегда доступен тест-процессу): персистентность ключей
        // middleware-тестам не важна. Так же, как Program.cs гейтит на "Test" Hangfire/сидеры.
        var keyDirectory = environment.IsEnvironment("Test")
            ? Path.Combine(Path.GetTempPath(), "MitLicenseCenter", "test-keys")
            : environment.IsDevelopment()
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MitLicenseCenter", "keys")
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "MitLicenseCenter", "keys");

        Directory.CreateDirectory(keyDirectory);

        services
            .AddDataProtection()
            .SetApplicationName("MitLicenseCenter")
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
    }
}
