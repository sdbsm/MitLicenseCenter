using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

public sealed class LicenseUsageReportsTests
{
    // Фиксированные «сейчас» и база бакетов — детерминизм дефолтного диапазона и порядка.
    private static readonly DateTime Now = new(2026, 6, 6, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Bucket0 = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

    private static TimeProvider Clock => TestHelpers.FixedClock(Now);

    private static LicenseUsageSnapshot Snapshot(
        Guid? tenantId, DateTime bucketStartUtc, int min, int max, double avg, int limit) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BucketStartUtc = bucketStartUtc,
            ConsumedMin = min,
            ConsumedMax = max,
            ConsumedAvg = avg,
            Limit = limit,
        };

    private static LicenseUsageSeriesResponse Body(
        Results<Ok<LicenseUsageSeriesResponse>, ValidationProblem> result) =>
        ((Ok<LicenseUsageSeriesResponse>)result.Result).Value!;

    [Fact]
    public async Task Summary_empty_database_yields_empty_series_and_default_range()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await ReportsEndpoints.SummaryAsync(db, Clock, from: null, to: null, CancellationToken.None);
        var body = Body(result);

        body.Buckets.Should().BeEmpty();
        body.PeakConsumed.Should().Be(0);
        body.PeakLimit.Should().Be(0);
        body.PeakAtUtc.Should().BeNull();
        body.AverageConsumed.Should().Be(0);
        // Дефолт: to=now, from=now-7d.
        body.ToUtc.Should().Be(Now);
        body.FromUtc.Should().Be(Now - TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task Summary_sums_tenants_per_bucket_and_finds_peak()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        // Bucket0: t1(max5,avg4,lim10) + t2(max3,avg2,lim20) → max8, avg6, lim30.
        // Bucket1 (+15m): t1(max7,avg6,lim10) → max7, avg6, lim10. Пик ConsumedMax = 8 в Bucket0.
        var bucket1 = Bucket0.AddMinutes(15);
        db.LicenseUsageSnapshots.AddRange(
            Snapshot(t1, Bucket0, 1, 5, 4.0, 10),
            Snapshot(t2, Bucket0, 1, 3, 2.0, 20),
            Snapshot(t1, bucket1, 2, 7, 6.0, 10));
        await db.SaveChangesAsync();

        var from = Bucket0.AddHours(-1);
        var to = Bucket0.AddHours(1);
        var result = await ReportsEndpoints.SummaryAsync(db, Clock, from, to, CancellationToken.None);
        var body = Body(result);

        body.Buckets.Should().HaveCount(2);
        var b0 = body.Buckets[0];
        b0.BucketStartUtc.Should().Be(Bucket0);
        b0.ConsumedMax.Should().Be(8);
        b0.ConsumedAvg.Should().Be(6.0);
        b0.Limit.Should().Be(30);

        body.PeakConsumed.Should().Be(8);
        body.PeakLimit.Should().Be(30);
        body.PeakAtUtc.Should().Be(Bucket0);
        body.AverageConsumed.Should().Be(6.0); // (6 + 6) / 2
    }

    [Fact]
    public async Task Summary_includes_orphaned_rows_with_null_tenant()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var t1 = Guid.NewGuid();
        // Осиротевшая запись (TenantId=null) должна суммироваться в общий итог.
        db.LicenseUsageSnapshots.AddRange(
            Snapshot(t1, Bucket0, 1, 4, 3.0, 10),
            Snapshot(null, Bucket0, 1, 6, 5.0, 0));
        await db.SaveChangesAsync();

        var result = await ReportsEndpoints.SummaryAsync(
            db, Clock, Bucket0.AddHours(-1), Bucket0.AddHours(1), CancellationToken.None);
        var body = Body(result);

        body.Buckets.Should().HaveCount(1);
        body.Buckets[0].ConsumedMax.Should().Be(10); // 4 + 6 (orphan включён)
        body.Buckets[0].ConsumedAvg.Should().Be(8.0); // 3 + 5
        body.PeakConsumed.Should().Be(10);
    }

    [Fact]
    public async Task Drilldown_filters_to_one_tenant_with_stored_values()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        db.LicenseUsageSnapshots.AddRange(
            Snapshot(t1, Bucket0, 1, 5, 4.5, 10),
            Snapshot(t2, Bucket0, 2, 9, 7.0, 20));
        await db.SaveChangesAsync();

        var result = await ReportsEndpoints.DrilldownAsync(
            db, Clock, t1, Bucket0.AddHours(-1), Bucket0.AddHours(1), CancellationToken.None);
        var body = Body(result);

        body.Buckets.Should().HaveCount(1);
        // Хранимые значения тенанта как есть, без суммирования с t2.
        body.Buckets[0].ConsumedMax.Should().Be(5);
        body.Buckets[0].ConsumedAvg.Should().Be(4.5);
        body.Buckets[0].Limit.Should().Be(10);
        body.PeakConsumed.Should().Be(5);
    }

    [Fact]
    public async Task Drilldown_unknown_tenant_yields_empty_series()
    {
        using var db = TestHelpers.NewInMemoryDb();
        db.LicenseUsageSnapshots.Add(Snapshot(Guid.NewGuid(), Bucket0, 1, 5, 4.0, 10));
        await db.SaveChangesAsync();

        var result = await ReportsEndpoints.DrilldownAsync(
            db, Clock, Guid.NewGuid(), Bucket0.AddHours(-1), Bucket0.AddHours(1), CancellationToken.None);

        Body(result).Buckets.Should().BeEmpty();
    }

    [Fact]
    public async Task Series_ordered_by_bucket_start_ascending()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var t1 = Guid.NewGuid();
        // Вставляем не по порядку — ответ должен прийти по возрастанию BucketStartUtc.
        db.LicenseUsageSnapshots.AddRange(
            Snapshot(t1, Bucket0.AddMinutes(30), 1, 3, 2.0, 10),
            Snapshot(t1, Bucket0, 1, 5, 4.0, 10),
            Snapshot(t1, Bucket0.AddMinutes(15), 1, 4, 3.0, 10));
        await db.SaveChangesAsync();

        var result = await ReportsEndpoints.DrilldownAsync(
            db, Clock, t1, Bucket0.AddHours(-1), Bucket0.AddHours(1), CancellationToken.None);
        var body = Body(result);

        body.Buckets.Select(b => b.BucketStartUtc).Should().ContainInOrder(
            Bucket0, Bucket0.AddMinutes(15), Bucket0.AddMinutes(30));
    }

    [Fact]
    public async Task Range_filter_excludes_rows_outside_window()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var t1 = Guid.NewGuid();
        db.LicenseUsageSnapshots.AddRange(
            Snapshot(t1, Bucket0.AddHours(-2), 1, 9, 9.0, 10), // до окна
            Snapshot(t1, Bucket0, 1, 5, 4.0, 10),              // в окне
            Snapshot(t1, Bucket0.AddHours(2), 1, 8, 8.0, 10)); // после окна
        await db.SaveChangesAsync();

        var result = await ReportsEndpoints.SummaryAsync(
            db, Clock, Bucket0.AddHours(-1), Bucket0.AddHours(1), CancellationToken.None);
        var body = Body(result);

        body.Buckets.Should().HaveCount(1);
        body.Buckets[0].BucketStartUtc.Should().Be(Bucket0);
    }

    [Fact]
    public async Task To_before_from_is_validation_problem()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await ReportsEndpoints.SummaryAsync(
            db, Clock, from: Now, to: Now.AddDays(-1), CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    [Fact]
    public async Task Range_wider_than_max_span_clamps_from_forward()
    {
        using var db = TestHelpers.NewInMemoryDb();

        // Запрошено 100 дней — кламп до 31, from двигается вперёд к to-31d.
        var to = Now;
        var from = Now.AddDays(-100);
        var result = await ReportsEndpoints.SummaryAsync(db, Clock, from, to, CancellationToken.None);
        var body = Body(result);

        body.ToUtc.Should().Be(to);
        body.FromUtc.Should().Be(to - TimeSpan.FromDays(31));
    }
}
