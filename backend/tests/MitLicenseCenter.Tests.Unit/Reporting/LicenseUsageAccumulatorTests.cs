using FluentAssertions;
using MitLicenseCenter.Application.Reporting;
using MitLicenseCenter.Infrastructure.Reporting;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Reporting;

// MLC-048 (ADR-25): аккумулятор копит мгновенные семплы потребления в текущем 15-мин
// бакете и на границе бакета возвращает агрегаты (min/max/avg) прошлого. Чистая логика —
// без БД.
public sealed class LicenseUsageAccumulatorTests
{
    private static readonly Guid T1 = Guid.NewGuid();
    private static readonly Guid T2 = Guid.NewGuid();

    // Граница 15-мин бакета выровнена на четверти часа (:00/:15/:30/:45).
    private static readonly DateTime Bucket0 = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

    private static LicenseUsageSample S(Guid tenant, int consumed, int limit = 10) =>
        new(tenant, consumed, limit);

    [Fact]
    public void Samples_within_one_bucket_return_no_rows()
    {
        var acc = new LicenseUsageAccumulator();

        acc.RecordSample(Bucket0, [S(T1, 3)]).Should().BeEmpty();
        acc.RecordSample(Bucket0.AddMinutes(5), [S(T1, 5)]).Should().BeEmpty();
        acc.RecordSample(Bucket0.AddMinutes(14), [S(T1, 4)]).Should().BeEmpty();
    }

    [Fact]
    public void Crossing_the_boundary_flushes_previous_bucket_with_min_max_avg()
    {
        var acc = new LicenseUsageAccumulator();

        // Семплы внутри bucket0 (метки внутри одной четверти часа).
        acc.RecordSample(Bucket0.AddMinutes(1), [S(T1, 2)]);
        acc.RecordSample(Bucket0.AddMinutes(6), [S(T1, 8)]);
        acc.RecordSample(Bucket0.AddMinutes(11), [S(T1, 5)]);

        // Первый семпл следующего бакета → флаш закрытого bucket0.
        var flushed = acc.RecordSample(Bucket0.AddMinutes(15), [S(T1, 1)]);

        flushed.Should().ContainSingle();
        var row = flushed[0];
        row.TenantId.Should().Be(T1);
        row.BucketStartUtc.Should().Be(Bucket0);
        row.ConsumedMin.Should().Be(2);
        row.ConsumedMax.Should().Be(8);
        row.ConsumedAvg.Should().BeApproximately((2 + 8 + 5) / 3.0, 1e-9);
        row.Limit.Should().Be(10);
    }

    [Fact]
    public void Sample_timestamp_is_floored_to_the_quarter_hour_bucket()
    {
        var acc = new LicenseUsageAccumulator();

        acc.RecordSample(Bucket0.AddMinutes(7).AddSeconds(42), [S(T1, 3)]);
        var flushed = acc.RecordSample(Bucket0.AddMinutes(16), [S(T1, 9)]);

        flushed.Should().ContainSingle();
        flushed[0].BucketStartUtc.Should().Be(Bucket0, "10:07:42 округляется вниз к 10:00");
    }

    [Fact]
    public void Idle_tenant_sample_is_counted_and_pulls_min_and_avg_down()
    {
        var acc = new LicenseUsageAccumulator();

        acc.RecordSample(Bucket0.AddMinutes(1), [S(T1, 0)]); // идл
        acc.RecordSample(Bucket0.AddMinutes(6), [S(T1, 4)]);
        var flushed = acc.RecordSample(Bucket0.AddMinutes(15), [S(T1, 0)]);

        var row = flushed.Single();
        row.ConsumedMin.Should().Be(0);
        row.ConsumedMax.Should().Be(4);
        row.ConsumedAvg.Should().BeApproximately(2.0, 1e-9, "идл (0) включён в среднее: (0+4)/2");
    }

    [Fact]
    public void Limit_in_a_bucket_is_the_last_observed_value()
    {
        var acc = new LicenseUsageAccumulator();

        acc.RecordSample(Bucket0.AddMinutes(1), [S(T1, 3, limit: 10)]);
        acc.RecordSample(Bucket0.AddMinutes(6), [S(T1, 3, limit: 20)]);
        var flushed = acc.RecordSample(Bucket0.AddMinutes(15), [S(T1, 1)]);

        flushed.Single().Limit.Should().Be(20, "лимит в бакете — последнее наблюдённое значение");
    }

    [Fact]
    public void Flush_emits_one_row_per_tenant_present_in_the_closed_bucket()
    {
        var acc = new LicenseUsageAccumulator();

        acc.RecordSample(Bucket0.AddMinutes(1), [S(T1, 2), S(T2, 7)]);
        acc.RecordSample(Bucket0.AddMinutes(6), [S(T1, 4), S(T2, 5)]);

        var flushed = acc.RecordSample(Bucket0.AddMinutes(15), [S(T1, 1), S(T2, 1)]);

        flushed.Should().HaveCount(2);
        flushed.Single(r => r.TenantId == T1).ConsumedMax.Should().Be(4);
        flushed.Single(r => r.TenantId == T2).ConsumedMin.Should().Be(5);
    }

    [Fact]
    public void Bucket_started_by_one_tenant_does_not_resurrect_after_flush()
    {
        var acc = new LicenseUsageAccumulator();

        acc.RecordSample(Bucket0.AddMinutes(1), [S(T1, 3)]);
        acc.RecordSample(Bucket0.AddMinutes(15), [S(T1, 9)]);   // флаш bucket0, старт bucket1

        // Внутри bucket1 — пусто, прошлое состояние сброшено.
        var second = acc.RecordSample(Bucket0.AddMinutes(20), [S(T1, 9)]);
        second.Should().BeEmpty();

        // Флаш bucket1: только семплы bucket1 (9 и 9), без следов bucket0 (3).
        var flushed = acc.RecordSample(Bucket0.AddMinutes(30), [S(T1, 1)]);
        var row = flushed.Single();
        row.ConsumedMin.Should().Be(9);
        row.ConsumedMax.Should().Be(9);
    }

    [Fact]
    public void Clock_moving_backwards_is_ignored_and_does_not_flush()
    {
        var acc = new LicenseUsageAccumulator();

        acc.RecordSample(Bucket0.AddMinutes(20), [S(T1, 5)]);   // bucket1 (10:15)
        var flushed = acc.RecordSample(Bucket0.AddMinutes(1), [S(T1, 99)]); // откат в bucket0

        flushed.Should().BeEmpty("семпл из прошлого бакета игнорируется (best-effort)");

        // Семпл «99» не должен попасть в агрегат: флаш bucket1 даёт только 5.
        var next = acc.RecordSample(Bucket0.AddMinutes(31), [S(T1, 1)]);
        next.Single().ConsumedMax.Should().Be(5);
    }
}
