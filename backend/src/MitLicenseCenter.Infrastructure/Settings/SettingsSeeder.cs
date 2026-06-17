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
    // EventId конвенция SettingsSeeder: диапазон 110x.
    // 1100 — засеяны новые параметры.
    [LoggerMessage(
        EventId = 1100,
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

        await HealRasEndpointAsync(db, now, ct).ConfigureAwait(false);
        await HealRasAgentPortAsync(db, now, ct).ConfigureAwait(false);
    }

    // MLC-117: целевой heal апгрейда. На БД, засеянной до того, как у OneC.RAS.Endpoint
    // появился сидовый дефолт, строка ключа уже существует с пустым ValueText — сидер
    // (трогает только ОТСУТСТВУЮЩИЕ ключи) её обойдёт, и публикация будет падать
    // «Не задан адрес 1С-кластера». Под single-host (ADR-28) пустой RAS endpoint —
    // всегда сломанное состояние, поэтому одноразово проставляем дефолт. Идемпотентно:
    // непустые значения и другие ключи не трогаем; дефолт берём из каталога (одна точка
    // истины), а не хардкодим литерал второй раз.
    private static async Task HealRasEndpointAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var defaultValue = SettingDefinitions.All[SettingKey.OneCRasEndpoint].DefaultValue;
        if (string.IsNullOrWhiteSpace(defaultValue))
        {
            return;
        }

        var entry = await db.Settings
            .SingleOrDefaultAsync(s => s.Key == SettingKey.OneCRasEndpoint, ct)
            .ConfigureAwait(false);

        // Лечим только реально пустую строку (plain без значения): ValueText пустой и
        // зашифрованный Value отсутствует. Если оператор уже задал endpoint — не трогаем.
        if (entry is null ||
            !string.IsNullOrWhiteSpace(entry.ValueText) ||
            entry.Value is not null)
        {
            return;
        }

        entry.ValueText = defaultValue;
        entry.UpdatedBy = "System";
        entry.UpdatedAt = now;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // MLC-194: тот же heal апгрейда для OneC.RAS.AgentPort (порт агента кластера ragent).
    // На БД, засеянной до появления этого ключа, строки нет вовсе → её создаст сидер выше
    // (с дефолтом 1540). Heal страхует случай, когда строка существует с пустым ValueText
    // (например, после ручного очищения): без порта агента авто-регистрация RAS соберёт
    // адрес без порта. Идемпотентно: непустые значения и другие ключи не трогаем; дефолт —
    // из каталога (одна точка истины).
    private static async Task HealRasAgentPortAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        var defaultValue = SettingDefinitions.All[SettingKey.OneCRasAgentPort].DefaultValue;
        if (string.IsNullOrWhiteSpace(defaultValue))
        {
            return;
        }

        var entry = await db.Settings
            .SingleOrDefaultAsync(s => s.Key == SettingKey.OneCRasAgentPort, ct)
            .ConfigureAwait(false);

        if (entry is null ||
            !string.IsNullOrWhiteSpace(entry.ValueText) ||
            entry.Value is not null)
        {
            return;
        }

        entry.ValueText = defaultValue;
        entry.UpdatedBy = "System";
        entry.UpdatedAt = now;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
