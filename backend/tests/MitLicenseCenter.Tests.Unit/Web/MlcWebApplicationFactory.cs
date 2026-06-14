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

// MLC-125 — общий WebApplicationFactory для интеграционных тестов middleware-пайплайна
// (security-заголовки + rate-limiter). Поднимает реальный хост из Program.cs под средой
// "Test", подменяя через DI всё, что иначе ходило бы в реальную инфраструктуру:
//   • Среда "Test"      → Program.cs пропускает регистрацию рекуррентных Hangfire-джоб и
//                         сидеров (IdentitySeeder/SettingsSeeder): и то и другое требует
//                         реального SQL Server (RecurringJob.AddOrUpdate → JobStorage.Current
//                         (SqlServerStorage); IdentitySeeder → EF MigrateAsync). Это позволяет
//                         хосту подняться на EF InMemory без БД и без Hangfire-стораджа.
//   • ConnectionStrings → фейковые непустые (см. ниже) — нужны лишь чтобы пройти гейты
//                         AddInfrastructure/AddHangfire, которые бросают на пустой строке;
//                         к реальной БД они не ведут.
//   • AppDbContext      → EF InMemory (нет SQL Server).
//   • IHostedService'ы  → удалены (BackgroundJobServer, HotTier/RasHealth/BackupPump/PerfRecording
//                         ходят в SQL/1С/WMI и упали бы при старте).
//
// Стратегия строк подключения:
//   ConnectionStrings:Default  = "Server=.;Encrypt=false;" — непустая (проходит гейты),
//                                без InitialCatalog → DatabaseBootstrapper.GetDatabaseName()=="" → no-op.
//   ConnectionStrings:Hangfire = "Server=.;Encrypt=false;" — непустая (AddHangfire не бросает);
//                                JobStorage никогда не используется (джобы не регистрируются в "Test",
//                                BackgroundJobServer удалён) → реального коннекта к SQL нет.
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
