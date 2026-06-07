using System.Data;
using Microsoft.Data.SqlClient;
using MitLicenseCenter.Domain.Tenants;

namespace MitLicenseCenter.Tools.PerfHarness;

// Засев истории использования лицензий (dbo.LicenseUsageSnapshots, MLC-048/ADR-25) для
// наполнения графиков /reports. Объём большой (дни × 96 бакетов × тенанты ≈ миллионы строк) →
// пишем через SqlBulkCopy чанками, минуя EF change-tracking. Модель потребления — в чистом
// SeedDataGenerator.BuildUsageSample.
//   • realistic → Limit строки = лимиту тенанта (coupled, как в проде ReconciliationJob),
//     consumed относительно него (профиль задаёт over-limit);
//   • perf      → decoupled Limit = UsageLimitFor (поведение MLC-039-надстройки 1:1).
internal static class UsageSeeder
{
    private const int ChunkSize = 100_000;
    private const int BucketMinutes = 15;

    public static async Task RunAsync(
        IReadOnlyList<Tenant> tenants,
        IReadOnlyList<TenantUsageProfile> profiles,
        bool realistic,
        int usageDays,
        int seed,
        string connectionString,
        DateTime nowUtc,
        TextWriter log,
        CancellationToken ct)
    {
        if (usageDays <= 0)
        {
            return;
        }

        // Кого сеять: realistic → профили (coupled Limit), perf → тенанты (decoupled UsageLimitFor).
        List<(Guid TenantId, int Index, int Limit, bool Over)> series = realistic
            ? profiles
                .Select(p => (p.TenantId, p.TenantIndex, p.Limit, p.OverLimit))
                .ToList()
            : tenants
                .Select((t, i) => (t.Id, i, SeedDataGenerator.UsageLimitFor(i), false))
                .ToList();

        if (series.Count == 0)
        {
            return;
        }

        // Окно выровнено на границу бакета (как у боевого аккумулятора) и ВКЛЮЧАЕТ текущий
        // бакет floor(now) последним — он совпадает с CurrentConsumed профиля (правый край
        // графика = live-снимок дашборда).
        var end = SeedDataGenerator.FloorToBucket(nowUtc);
        var bucketCount = usageDays * 24 * 60 / BucketMinutes;
        var firstBucket = end.AddMinutes(-BucketMinutes * (bucketCount - 1));
        var totalRows = (long)bucketCount * series.Count;
        log.WriteLine(
            $"Usage: {series.Count} тенантов × {bucketCount} бакетов " +
            $"({usageDays}д × {BucketMinutes}мин) = {totalRows} строк.");

        var rng = new Random(seed ^ 0x05A6E);
        var table = NewTable();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var bulk = new SqlBulkCopy(conn)
        {
            DestinationTableName = "dbo.LicenseUsageSnapshots",
            BulkCopyTimeout = 0,
            BatchSize = ChunkSize,
        };
        foreach (DataColumn col in table.Columns)
        {
            bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        long written = 0;
        foreach (var (tenantId, index, limit, over) in series)
        {
            var bucket = firstBucket;
            for (var b = 0; b < bucketCount; b++, bucket = bucket.AddMinutes(BucketMinutes))
            {
                var s = SeedDataGenerator.BuildUsageSample(index, limit, over, bucket);
                table.Rows.Add(
                    NextGuid(rng), tenantId, bucket,
                    s.ConsumedMin, s.ConsumedMax, s.ConsumedAvg, s.Limit);

                if (table.Rows.Count >= ChunkSize)
                {
                    await bulk.WriteToServerAsync(table, ct).ConfigureAwait(false);
                    written += table.Rows.Count;
                    table.Clear();
                    log.WriteLine($"  usage: {written} / {totalRows}");
                }
            }
        }

        if (table.Rows.Count > 0)
        {
            await bulk.WriteToServerAsync(table, ct).ConfigureAwait(false);
            written += table.Rows.Count;
        }

        log.WriteLine($"Seeded {written} license-usage snapshots.");
    }

    private static DataTable NewTable()
    {
        var t = new DataTable();
        t.Columns.Add("Id", typeof(Guid));
        t.Columns.Add("TenantId", typeof(Guid));
        t.Columns.Add("BucketStartUtc", typeof(DateTime));
        t.Columns.Add("ConsumedMin", typeof(int));
        t.Columns.Add("ConsumedMax", typeof(int));
        t.Columns.Add("ConsumedAvg", typeof(double));
        t.Columns.Add("Limit", typeof(int));
        return t;
    }

    private static Guid NextGuid(Random rng)
    {
        var bytes = new byte[16];
        rng.NextBytes(bytes);
        return new Guid(bytes);
    }
}
