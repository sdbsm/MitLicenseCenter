using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Settings;

// Идемпотентный seeder: на старте создаёт row'ы для отсутствующих в БД ключей
// из SettingDefinitions catalog'а. Plain ключи с DefaultValue получают значение
// сразу, секреты сидятся как `IsSet=false` (Value/ValueText = null) — оператор
// заполняет через UI «Параметры».
public static partial class SettingsSeeder
{
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Засеяно {Count} новых параметров.")]
    private static partial void LogSeeded(ILogger logger, int count);

    public static async Task EnsureSeededAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var clock = sp.GetRequiredService<TimeProvider>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SettingsSeeder).FullName!);

        var existing = await db.Settings.Select(s => s.Key).ToListAsync(ct).ConfigureAwait(false);
        var existingSet = new HashSet<string>(existing, StringComparer.Ordinal);

        var now = clock.GetUtcNow().UtcDateTime;
        var inserted = 0;
        foreach (var def in SettingDefinitions.All.Values)
        {
            if (existingSet.Contains(def.Key))
            {
                continue;
            }

            // Plain с дефолтом — кладём значение; всё остальное (секреты + plain без
            // дефолта) сеется «пустым», IsSet=false на read-side.
            var hasDefault = !def.IsSecret && def.DefaultValue is not null;
            db.Settings.Add(new SettingEntry
            {
                Key = def.Key,
                IsSecret = def.IsSecret,
                Description = def.Description,
                ValueText = hasDefault ? def.DefaultValue : null,
                Value = null,
                UpdatedAt = now,
                UpdatedBy = "System",
            });
            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            LogSeeded(logger, inserted);
        }
    }
}
