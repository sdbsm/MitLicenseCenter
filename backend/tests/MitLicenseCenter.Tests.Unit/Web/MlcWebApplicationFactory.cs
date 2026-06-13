using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Tests.Unit.Web;

// MLC-125 — общий WebApplicationFactory для интеграционных тестов middleware-пайплайна.
// Загружает реальный хост из Program.cs, подменяя:
//   • ConnectionStrings → фейковые, не ведущие к реальной БД;
//   • AppDbContext      → EF InMemory (нет SQL Server);
//   • Hangfire          → UseInMemoryStorage (Hangfire.InMemory v1.0) — нет SQL;
//   • Hangfire server   → удалён (BackgroundJobServer не стартует);
//   • Seeders           → работают поверх InMemory DbContext (IdentitySeeder + SettingsSeeder).
//
// Стратегия строк подключения:
//   ConnectionStrings:Default  = "Server=.;Encrypt=false;" (без InitialCatalog → DatabaseBootstrapper no-op)
//   ConnectionStrings:Hangfire = "Server=.;Encrypt=false;" (без InitialCatalog → подменяется InMemory)
// AddInfrastructure получает непустую строку → проходит свой гейт; потом DbContext заменяется.
// AddHangfire(.UseSqlServerStorage(..)) регистрируется из Program.cs, но потом ConfigureTestServices
// переопределяет GlobalConfiguration на InMemory — RecurringJob.AddOrUpdate видит InMemory storage.
public sealed class MlcWebApplicationFactory : WebApplicationFactory<MitLicenseCenter.Web.Program>
{
    // Фейковая строка: непустая (гейт AddInfrastructure/Hangfire проходит),
    // но без InitialCatalog → DatabaseBootstrapper.GetDatabaseName() вернёт "" → no-op.
    private const string FakeConnectionString = "Server=.;Encrypt=false;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Среда "Test" — специальный гейт в Program.cs пропускает RecurringJob.AddOrUpdate
        // и Seeders (они требуют SQL Server), что позволяет хосту подняться с InMemory DbContext.
        // В "Test" env: Swagger выключен (как в Production), HSTS/redirect выключен — нам ОК.
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration(config =>
        {
            // Переопределяем connection strings в конце — они имеют наивысший приоритет.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Непустая строка без InitialCatalog:
                //   • AddInfrastructure: получает непустую строку → проходит гейт (no-throw)
                //   • DatabaseBootstrapper: GetDatabaseName() вернёт "" → no-op (не коннектится)
                //   • AddHangfire.UseSqlServerStorage: регистрируется, но переопределяется ниже
                ["ConnectionStrings:Default"] = FakeConnectionString,
                ["ConnectionStrings:Hangfire"] = FakeConnectionString,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // ── 1. Заменяем AppDbContext на EF InMemory ────────────────────────────────────
            // Убираем регистрацию SQL Server DbContext, добавленную AddInfrastructure.
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
            {
                services.Remove(dbDescriptor);
            }

            services.RemoveAll<AppDbContext>();

            // Добавляем InMemory DbContext (уникальное имя на каждую фабрику)
            var dbName = $"mlc-test-{Guid.NewGuid():N}";
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            // ── 2. Убираем все IHostedService ─────────────────────────────────────────────
            // BackgroundService'ы приложения ходят в SQL/1С/WMI и падают при старте без реальной
            // инфраструктуры:
            //   • BackgroundJobServer (Hangfire) — SQL Server
            //   • HotTierPollingService — AppDbContext (InMemory OK, но тикает немедленно)
            //   • RasHealthProbingService — запускает rac.exe (недоступен в тест-среде)
            //   • BackupPumpService — AppDbContext + IBackupOrchestrator
            //   • PerfRecordingSamplingService — WMI/PerformanceCounter (Windows-only, CA1416)
            // Для тестов middleware нам IHostedService не нужны.
            var allHostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();

            foreach (var descriptor in allHostedServices)
            {
                services.Remove(descriptor);
            }
        });
    }
}
