using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Application.Identity;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Reporting;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Clusters;
using MitLicenseCenter.Infrastructure.Diagnostics;
using MitLicenseCenter.Infrastructure.Discovery;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Publishing;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Infrastructure.Settings;

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
        services.AddSingleton<ColdThrottleState>();

        // License usage accumulator (MLC-048, ADR-25): singleton — состояние текущего
        // 15-мин бакета переживает scoped-инвокации cold-джобы (как ColdThrottleState).
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

        // Hot-tier polling: BackgroundService для sub-minute hot-poll (Hangfire
        // CRON minimum = 1 мин, а нам нужно 3–5s). См. ADR-6.1.
        services.AddSingleton<HotTierPollingService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<HotTierPollingService>());

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
        var keyDirectory = environment.IsDevelopment()
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
