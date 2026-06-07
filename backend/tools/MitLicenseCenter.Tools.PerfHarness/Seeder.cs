using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Tools.PerfHarness;

// MLC-039 (PERF-03): персист графа через РЕАЛЬНЫЙ AppDbContext → FK, уникальные индексы и
// конверсии enum'ов соблюдаются самой моделью (миграции не трогаются). Аудит вставляется
// батчами с отключённым change-tracking — K в сотни тысяч–миллион не должно разорвать память.
internal static class Seeder
{
    private const int AuditBatchSize = 10_000;

    public static async Task RunAsync(
        SeedOptions opts,
        string connectionString,
        string scenarioPath,
        TextWriter log,
        CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var graph = SeedDataGenerator.Build(opts, nowUtc);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using (var db = new AppDbContext(options))
        {
            // Порядок: Tenants → Infobases (FK TenantId) → Publications (FK 1:1 InfobaseId).
            db.Tenants.AddRange(graph.Tenants);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            db.Infobases.AddRange(graph.Infobases);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            db.Publications.AddRange(graph.Publications);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        log.WriteLine($"Seeded {graph.Tenants.Count} tenants, {graph.Infobases.Count} infobases + publications.");

        var tenantIds = graph.Tenants.Select(t => t.Id).ToList();
        var inserted = 0;
        var buffer = new List<AuditLog>(AuditBatchSize);
        foreach (var row in SeedDataGenerator.EnumerateAuditLogs(opts, tenantIds, nowUtc))
        {
            buffer.Add(row);
            if (buffer.Count >= AuditBatchSize)
            {
                await FlushAuditAsync(options, buffer, ct).ConfigureAwait(false);
                inserted += buffer.Count;
                buffer.Clear();
                log.WriteLine($"  audit: {inserted} / {opts.Audit}");
            }
        }
        if (buffer.Count > 0)
        {
            await FlushAuditAsync(options, buffer, ct).ConfigureAwait(false);
            inserted += buffer.Count;
        }
        log.WriteLine($"Seeded {inserted} audit rows.");

        // История использования лицензий для графиков /reports (выкл по умолчанию: usageDays=0).
        await UsageSeeder.RunAsync(graph.Tenants, opts.UsageDays, opts.Seed, connectionString, log, ct)
            .ConfigureAwait(false);

        await ScenarioFile.SaveAsync(graph.Scenario, scenarioPath, ct).ConfigureAwait(false);
        log.WriteLine($"Scenario written: {scenarioPath} ({graph.Scenario.Sessions.Count} sessions).");
    }

    private static async Task FlushAuditAsync(
        DbContextOptions<AppDbContext> options, List<AuditLog> rows, CancellationToken ct)
    {
        await using var db = new AppDbContext(options);
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        db.AuditLogs.AddRange(rows);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
