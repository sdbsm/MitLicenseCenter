using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Reporting;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Infrastructure.Reporting.Testing;
using MitLicenseCenter.Tests.Unit.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-185c: джоба сбора размеров баз. Охват — только базы инфобаз: один замер probe
// фильтруется по DatabaseName инфобаз, пишется DatabaseSizeSnapshot с единым SnapshotAtUtc.
// Реальный реляционный провайдер (SQLite) — case-insensitive свёртка имён делается в памяти
// (StringComparer.OrdinalIgnoreCase), не EF-запросом.
public sealed class DatabaseSizeCollectionJobTests
{
    private static readonly DateTime Now = new(2026, 6, 17, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Writes_snapshots_only_for_infobase_databases_with_a_reading()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var probe = new FakeDatabaseSizeProbe();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using (var seed = sqlite.NewContext())
        {
            SeedTenant(seed, tenantA, "Acme");
            SeedTenant(seed, tenantB, "Globex");
            // Две инфобазы с показаниями + одна без показания (база отсутствует/недоступна).
            SeedInfobase(seed, tenantA, "db_acme");
            SeedInfobase(seed, tenantB, "db_globex");
            SeedInfobase(seed, tenantA, "db_missing");
            await seed.SaveChangesAsync();
        }

        probe.NextReadings =
        [
            new DatabaseSizeReading("db_acme", DataBytes: 1000, LogBytes: 100),
            new DatabaseSizeReading("db_globex", DataBytes: 2000, LogBytes: 200),
            // Базы инстанса вне инфобаз — игнорируются.
            new DatabaseSizeReading("master", DataBytes: 9999, LogBytes: 9999),
            new DatabaseSizeReading("db_orphan", DataBytes: 5555, LogBytes: 555),
        ];

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, probe).RunAsync(CancellationToken.None);
        }

        probe.ReadCount.Should().Be(1, "ровно один замер за прогон");

        using var verify = sqlite.NewContext();
        var rows = verify.DatabaseSizeSnapshots.OrderBy(x => x.DatabaseName).ToList();
        rows.Select(x => x.DatabaseName).Should().BeEquivalentTo(["db_acme", "db_globex"],
            "снимки только для баз инфобаз, имеющих показание; db_missing и db_orphan/master — нет");

        var acme = rows.Single(x => x.DatabaseName == "db_acme");
        acme.TenantId.Should().Be(tenantA);
        acme.DataBytes.Should().Be(1000);
        acme.LogBytes.Should().Be(100);

        var globex = rows.Single(x => x.DatabaseName == "db_globex");
        globex.TenantId.Should().Be(tenantB);
        globex.DataBytes.Should().Be(2000);
        globex.LogBytes.Should().Be(200);

        rows.Select(x => x.SnapshotAtUtc).Distinct().Should().ContainSingle()
            .Which.Should().Be(Now, "единый момент замера = clock.UtcNow на старте прогона");
    }

    [Fact]
    public async Task Matches_database_name_case_insensitively()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var probe = new FakeDatabaseSizeProbe();

        var tenant = Guid.NewGuid();
        using (var seed = sqlite.NewContext())
        {
            SeedTenant(seed, tenant, "Acme");
            SeedInfobase(seed, tenant, "DbA"); // инфобаза в одном регистре
            await seed.SaveChangesAsync();
        }

        // Показание в другом регистре — должно совпасть.
        probe.NextReadings = [new DatabaseSizeReading("dba", DataBytes: 42, LogBytes: 7)];

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, probe).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        var row = verify.DatabaseSizeSnapshots.Single();
        row.TenantId.Should().Be(tenant);
        row.DataBytes.Should().Be(42);
        row.LogBytes.Should().Be(7);
        row.DatabaseName.Should().Be("DbA",
            "пишем каноничное имя из инфобазы, а не регистр из показания SQL");
    }

    [Fact]
    public async Task Writes_nothing_when_no_readings_match()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var probe = new FakeDatabaseSizeProbe();

        var tenant = Guid.NewGuid();
        using (var seed = sqlite.NewContext())
        {
            SeedTenant(seed, tenant, "Acme");
            SeedInfobase(seed, tenant, "db_acme");
            await seed.SaveChangesAsync();
        }

        probe.NextReadings = [new DatabaseSizeReading("db_other", DataBytes: 1, LogBytes: 1)];

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, probe).RunAsync(CancellationToken.None);
        }

        probe.ReadCount.Should().Be(1);
        using var verify = sqlite.NewContext();
        verify.DatabaseSizeSnapshots.Count().Should().Be(0);
    }

    private static DatabaseSizeCollectionJob NewJob(AppDbContext db, FakeDatabaseSizeProbe probe) =>
        new(db, probe, TestHelpers.FixedClock(Now), NullLogger<DatabaseSizeCollectionJob>.Instance);

    private static void SeedTenant(AppDbContext db, Guid id, string name) =>
        db.Tenants.Add(new Tenant
        {
            Id = id,
            Name = name,
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = Now,
        });

    private static void SeedInfobase(AppDbContext db, Guid tenantId, string databaseName) =>
        db.Infobases.Add(new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = databaseName,
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseName = databaseName,
            CreatedAt = Now,
        });
}
