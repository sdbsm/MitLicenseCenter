using System.Data;
using Microsoft.Data.SqlClient;
using MitLicenseCenter.Domain.Tenants;

namespace MitLicenseCenter.Tools.PerfHarness;

// Засев истории использования лицензий (dbo.LicenseUsageSnapshots, MLC-048/ADR-25) для
// наполнения графиков /reports. Объём большой (дни × 96 бакетов × тенанты ≈ миллионы строк) →
// пишем через SqlBulkCopy чанками, минуя EF change-tracking. Модель потребления и per-tenant
// потолок — в чистом SeedDataGenerator.BuildUsageSample (отвязаны от enforcement-лимита).
internal static class UsageSeeder
{
    private const int ChunkSize = 100_000;
    private const int BucketMinutes = 15;

    public static async Task RunAsync(
        IReadOnlyList<Tenant> tenants,
        int usageDays,
        int seed,
        string connectionString,
        TextWriter log,
        CancellationToken ct)
    {
        if (usageDays <= 0 || tenants.Count == 0)
        {
            return;
        }

        // Окно выровнено на границу бакета (как у боевого аккумулятора): [now-usageDays; now).
        var end = FloorToBucket(DateTime.UtcNow);
        var start = end.AddDays(-usageDays);
        var bucketCount = (int)((end - start).TotalMinutes / BucketMinutes);
        var totalRows = (long)bucketCount * tenants.Count;
        log.WriteLine(
            $"Usage: {tenants.Count} тенантов × {bucketCount} бакетов " +
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
        for (var t = 0; t < tenants.Count; t++)
        {
            var tenantId = tenants[t].Id;
            var bucket = start;
            for (var b = 0; b < bucketCount; b++, bucket = bucket.AddMinutes(BucketMinutes))
            {
                var s = SeedDataGenerator.BuildUsageSample(t, bucket);
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

    private static DateTime FloorToBucket(DateTime utc)
    {
        var minute = utc.Minute - (utc.Minute % BucketMinutes);
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, minute, 0, DateTimeKind.Utc);
    }

    private static Guid NextGuid(Random rng)
    {
        var bytes = new byte[16];
        rng.NextBytes(bytes);
        return new Guid(bytes);
    }
}
