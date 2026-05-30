using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Clusters;
using MitLicenseCenter.Infrastructure.Discovery;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Publishing;
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

        // Settings: singleton snapshot (in-mem TTL ≈ 30s) + scoped store
        // (DbContext-bound). Mutate через store → store.Invalidate() сбрасывает snapshot.
        services.AddSingleton<ISettingsSnapshot, SettingsSnapshot>();
        services.AddScoped<ISettingsStore, SettingsStore>();

        // Cluster adapter: rac.exe wrapper — единственный 1С cluster-адаптер
        // (Stage 5 PR 5.1, ADR-16). REST adapter и Polly circuit breaker удалены —
        // они хеджировали primary, эмпирически отсутствующий на default-деплоях 1С 8.5.
        // CLI-контракт зафиксирован в ADR-3.3.
        services.AddSingleton<IRacProcessRunner, SystemProcessRacRunner>();
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
#pragma warning restore CA1416

        // Discovery (интерактивная настройка форм): SQL-перечисление БД и скан rac.exe.
        services.AddScoped<ISqlDatabaseDiscovery, SqlDatabaseDiscovery>();
        services.AddSingleton<IRacPathDiscovery, RacPathDiscovery>();
        services.AddSingleton<IPlatformVersionDiscovery, PlatformVersionDiscovery>();

        // Snapshot store + hot-tier registry (singletons, PR 3.3).
        services.AddSingleton<IActiveSessionSnapshotStore, ActiveSessionSnapshotStore>();
        services.AddSingleton<IHotTierRegistry, HotTierRegistry>();
        services.AddSingleton<ColdThrottleState>();

        // Reconciliation job + kill enforcer (scoped — require DbContext + IClusterClient).
        services.AddScoped<IReconciliationJob, ReconciliationJob>();
        services.AddScoped<IKillEnforcer, KillEnforcer>();

        // Drift check job (PR 3.5): scoped (зависит от DbContext + IAuditLogger),
        // плюс singleton throttle-state по аналогии с ColdThrottleState.
        services.AddSingleton<DriftThrottleState>();
        services.AddScoped<IDriftCheckJob, DriftCheckJob>();

        // Audit retention (PR 4.3): scoped (DbContext + IAuditLogger), без
        // throttle-state — CRON фиксирован 03:00 daily, не tuneable оператором.
        services.AddScoped<IAuditRetentionJob, AuditRetentionJob>();

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
