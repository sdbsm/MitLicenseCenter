using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-185e: read-API /reports/database-size (сводка + drill-down) — зеркало
// license-usage. Прогон на SQLite (НЕ InMemory): отчёт опирается на серверную
// агрегацию GroupBy/Sum и подзапрос Max(SnapshotAtUtc) («последний снимок периода») —
// InMemory маскирует трансляцию (урок MLC-008). Канон схемы — AppDbContext.OnModelCreating.
public sealed class DatabaseSizeReportsTests
{
    private static readonly DateTime Now = new(2026, 6, 6, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day0 = new(2026, 6, 4, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day1 = new(2026, 6, 5, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 6, 6, 3, 0, 0, DateTimeKind.Utc);

    private static TimeProvider Clock => TestHelpers.FixedClock(Now);

    private static Tenant Tenant(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        MaxConcurrentLicenses = 10,
        IsActive = true,
        CreatedAt = Day0,
    };

    private static DatabaseSizeSnapshot Snap(
        Guid? tenantId, string db, DateTime at, long data, long log) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DatabaseName = db,
            SnapshotAtUtc = at,
            DataBytes = data,
            LogBytes = log,
        };

    private static DatabaseSizeSeriesResponse SummaryBody(
        Results<Ok<DatabaseSizeSeriesResponse>, ValidationProblem> result) =>
        ((Ok<DatabaseSizeSeriesResponse>)result.Result).Value!;

    private static DatabaseSizeTenantSeriesResponse DrilldownBody(
        Results<Ok<DatabaseSizeTenantSeriesResponse>, ValidationProblem> result) =>
        ((Ok<DatabaseSizeTenantSeriesResponse>)result.Result).Value!;

    // Два клиента, по 2 базы каждый, на 3 даты. Возвращает их id (t1, t2).
    private static (Guid T1, Guid T2) Seed(TestHelpers.SqliteTestDb sqlite)
    {
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        using var db = sqlite.NewContext();
        db.Tenants.AddRange(Tenant(t1, "Acme"), Tenant(t2, "Beta"));
        db.DatabaseSizeSnapshots.AddRange(
            // Day0
            Snap(t1, "acme_a", Day0, 100, 10),
            Snap(t1, "acme_b", Day0, 200, 20),
            Snap(t2, "beta_a", Day0, 50, 5),
            // Day1
            Snap(t1, "acme_a", Day1, 150, 15),
            Snap(t1, "acme_b", Day1, 250, 25),
            Snap(t2, "beta_a", Day1, 60, 6),
            // Day2 (последний снимок периода)
            Snap(t1, "acme_a", Day2, 300, 30),
            Snap(t1, "acme_b", Day2, 400, 40),
            Snap(t2, "beta_a", Day2, 80, 8));
        db.SaveChanges();
        return (t1, t2);
    }

    [Fact]
    public async Task Summary_empty_database_yields_empty_series_and_default_range()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var result = await ReportsEndpoints.DatabaseSizeSummaryAsync(
            db, Clock, from: null, to: null, CancellationToken.None);
        var body = SummaryBody(result);

        body.Points.Should().BeEmpty();
        body.Tenants.Should().BeEmpty();
        body.ToUtc.Should().Be(Now);
        body.FromUtc.Should().Be(Now - TimeSpan.FromDays(7));
        body.MaxSpanDays.Should().Be(31);
    }

    [Fact]
    public async Task Summary_host_total_sums_all_databases_per_snapshot_ordered()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        Seed(sqlite);
        using var db = sqlite.NewContext();

        var result = await ReportsEndpoints.DatabaseSizeSummaryAsync(
            db, Clock, Day0.AddDays(-1), Day2.AddDays(1), CancellationToken.None);
        var body = SummaryBody(result);

        body.Points.Should().HaveCount(3);
        body.Points.Select(p => p.AtUtc).Should().ContainInOrder(Day0, Day1, Day2);

        // Day0: data 100+200+50=350, log 10+20+5=35, total 385.
        var p0 = body.Points[0];
        p0.DataBytes.Should().Be(350);
        p0.LogBytes.Should().Be(35);
        p0.TotalBytes.Should().Be(385);

        // Day2: data 300+400+80=780, log 30+40+8=78, total 858.
        var p2 = body.Points[2];
        p2.DataBytes.Should().Be(780);
        p2.LogBytes.Should().Be(78);
        p2.TotalBytes.Should().Be(858);
    }

    [Fact]
    public async Task Summary_tenant_breakdown_is_last_snapshot_with_names_and_counts()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        Seed(sqlite);
        using var db = sqlite.NewContext();

        var result = await ReportsEndpoints.DatabaseSizeSummaryAsync(
            db, Clock, Day0.AddDays(-1), Day2.AddDays(1), CancellationToken.None);
        var body = SummaryBody(result);

        // Разбивка — только Day2 (последний снимок периода), отсортирована по TotalBytes desc.
        body.Tenants.Should().HaveCount(2);

        var acme = body.Tenants[0];
        acme.TenantName.Should().Be("Acme");
        acme.DataBytes.Should().Be(700); // 300 + 400
        acme.LogBytes.Should().Be(70);   // 30 + 40
        acme.TotalBytes.Should().Be(770);
        acme.DatabaseCount.Should().Be(2);

        var beta = body.Tenants[1];
        beta.TenantName.Should().Be("Beta");
        beta.TotalBytes.Should().Be(88); // 80 + 8
        beta.DatabaseCount.Should().Be(1);
    }

    [Fact]
    public async Task Summary_orphaned_snapshot_yields_row_with_null_tenant_name()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var t1 = Guid.NewGuid();
        using (var db = sqlite.NewContext())
        {
            db.Tenants.Add(Tenant(t1, "Acme"));
            db.DatabaseSizeSnapshots.AddRange(
                Snap(t1, "acme_a", Day2, 100, 10),
                Snap(null, "orphan_db", Day2, 500, 50)); // тенант удалён (SetNull)
            db.SaveChanges();
        }

        using var read = sqlite.NewContext();
        var result = await ReportsEndpoints.DatabaseSizeSummaryAsync(
            read, Clock, Day2.AddDays(-1), Day2.AddDays(1), CancellationToken.None);
        var body = SummaryBody(result);

        // Осиротевший снимок включён в итог по хосту.
        body.Points.Should().ContainSingle();
        body.Points[0].TotalBytes.Should().Be(660); // 110 + 550

        // Разбивка содержит строку «без клиента» (TenantId=null, TenantName=null).
        body.Tenants.Should().HaveCount(2);
        var orphan = body.Tenants.Single(r => r.TenantId == null);
        orphan.TenantName.Should().BeNull();
        orphan.TotalBytes.Should().Be(550);
        orphan.DatabaseCount.Should().Be(1);
    }

    [Fact]
    public async Task Summary_wider_than_max_span_clamps_from_forward()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var to = Now;
        var from = Now.AddDays(-100);
        var result = await ReportsEndpoints.DatabaseSizeSummaryAsync(
            db, Clock, from, to, CancellationToken.None);
        var body = SummaryBody(result);

        body.ToUtc.Should().Be(to);
        body.FromUtc.Should().Be(to - TimeSpan.FromDays(31));
        body.Clamped.Should().BeTrue();
        body.MaxSpanDays.Should().Be(31);
    }

    [Fact]
    public async Task Summary_to_before_from_is_validation_problem()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var result = await ReportsEndpoints.DatabaseSizeSummaryAsync(
            db, Clock, from: Now, to: Now.AddDays(-1), CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    [Fact]
    public async Task Drilldown_returns_one_tenant_series_excluding_others()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var (t1, _) = Seed(sqlite);
        using var db = sqlite.NewContext();

        var result = await ReportsEndpoints.DatabaseSizeDrilldownAsync(
            db, Clock, t1, Day0.AddDays(-1), Day2.AddDays(1), CancellationToken.None);
        var body = DrilldownBody(result);

        body.Points.Should().HaveCount(3);
        body.Points.Select(p => p.AtUtc).Should().ContainInOrder(Day0, Day1, Day2);

        // Day0 только t1: data 100+200=300, log 10+20=30 (beta не примешан).
        body.Points[0].DataBytes.Should().Be(300);
        body.Points[0].LogBytes.Should().Be(30);
        body.Points[0].TotalBytes.Should().Be(330);

        // Разбивка по базам на последний снимок (Day2), по TotalBytes desc.
        body.Databases.Should().HaveCount(2);
        body.Databases[0].DatabaseName.Should().Be("acme_b"); // 440
        body.Databases[0].TotalBytes.Should().Be(440);
        body.Databases[1].DatabaseName.Should().Be("acme_a"); // 330
        body.Databases[1].TotalBytes.Should().Be(330);
    }

    [Fact]
    public async Task Drilldown_unknown_tenant_yields_empty_series()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        Seed(sqlite);
        using var db = sqlite.NewContext();

        var result = await ReportsEndpoints.DatabaseSizeDrilldownAsync(
            db, Clock, Guid.NewGuid(), Day0.AddDays(-1), Day2.AddDays(1), CancellationToken.None);
        var body = DrilldownBody(result);

        body.Points.Should().BeEmpty();
        body.Databases.Should().BeEmpty();
        // Честные границы даже на пустом ряду.
        body.FromUtc.Should().Be(Day0.AddDays(-1));
        body.ToUtc.Should().Be(Day2.AddDays(1));
    }
}
