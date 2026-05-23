using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Clusters;
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

        // Cluster adapters: singleton circuit-state + typed REST HttpClient +
        // scoped RAS stub + scoped resilient decorator (PR 3.2).
        services.AddSingleton<ClusterCircuitState>();
        services.AddSingleton<ICircuitStatusReader>(sp => sp.GetRequiredService<ClusterCircuitState>());

        // RemoveAllResilienceHandlers() — снимаем глобальный AddStandardResilienceHandler
        // с нашего именованного клиента, потому что resilience-политикой владеет
        // ResilientClusterClient (Polly circuit breaker в ClusterCircuitState).
        // EXTEXP0001: метод помечен [Experimental] в Microsoft.Extensions.Http.Resilience;
        // подавляем, т.к. сознательно используем его по плану PR 3.2 ADR-3.2.
#pragma warning disable EXTEXP0001
        services.AddHttpClient<OneCRestClusterClient>((_, _) => { })
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        // RAS fallback (PR 3.8): реальный rac.exe wrapper. Контракт CLI и парсер
        // зафиксированы в ADR-3.3. Stub переехал в Clusters/Testing/ и в production-DI
        // больше не регистрируется (только unit-тесты PR 3.2/3.3 берут его явно).
        services.AddSingleton<IRacProcessRunner, SystemProcessRacRunner>();
        services.AddScoped<IRasFallbackClusterClient, RacExecutableRasClusterClient>();

        // Явная фабрика: ResilientClusterClient берёт конкретный OneCRestClusterClient
        // как IClusterClient primary (из IHttpClientFactory), чтобы тесты могли
        // подставить любой fake через конструктор без зависимости от HttpClient.
        services.AddScoped<IClusterClient>(sp => new ResilientClusterClient(
            primary: sp.GetRequiredService<OneCRestClusterClient>(),
            fallback: sp.GetRequiredService<IRasFallbackClusterClient>(),
            state: sp.GetRequiredService<ClusterCircuitState>()));

        // IIS publishing: реальный адаптер ServerManager + XDocument (PR 3.5).
        // Stub переехал в Publishing/Testing/ для unit-тестов, в production-DI
        // не регистрируется — реальный OneCIisPublishingService требует Windows.
#pragma warning disable CA1416 // Validate platform compatibility — single-node deployment is Windows-only by design (memory/infrastructure_integration.md).
        services.AddScoped<IIisPublishingService, OneCIisPublishingService>();
#pragma warning restore CA1416

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
