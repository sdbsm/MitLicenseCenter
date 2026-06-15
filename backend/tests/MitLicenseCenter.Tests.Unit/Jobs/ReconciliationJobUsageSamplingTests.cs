using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Reporting;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-048 (ADR-25): врезка съёма телеметрии в cold ReconciliationJob. Бакетинг покрыт
// LicenseUsageAccumulatorTests — здесь проверяем именно проводку: семпл собирается по
// активным тенантам цикла, а строки персистятся ровно когда аккумулятор вернул бакет.
public sealed class ReconciliationJobUsageSamplingTests
{
    private static readonly DateTime Now = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunColdAsync_samples_active_tenants_and_persists_returned_buckets()
    {
        var tenantId = Guid.NewGuid();
        using var db = TestHelpers.NewInMemoryDb($"usage-sampling-{Guid.NewGuid():N}");
        await SeedActiveTenantWithInfobase(db, tenantId, limit: 10);

        var bucket = new LicenseUsageBucket(tenantId, Now, ConsumedMin: 1, ConsumedMax: 4, ConsumedAvg: 2.5, Limit: 10);
        var accumulator = new StubAccumulator([bucket]);

        var job = NewJob(db, accumulator);
        await job.RunColdAsync(CancellationToken.None);

        // Семпл собран по активному тенанту: сессий нет → consumed 0 (честный идл), лимит из тенанта.
        accumulator.LastSamples.Should().ContainSingle();
        var sample = accumulator.LastSamples!.Single();
        sample.TenantId.Should().Be(tenantId);
        sample.Consumed.Should().Be(0);
        sample.Limit.Should().Be(10);

        // Возвращённый аккумулятором бакет персистнут как строка телеметрии.
        var row = db.LicenseUsageSnapshots.Single();
        row.TenantId.Should().Be(tenantId);
        row.BucketStartUtc.Should().Be(Now);
        row.ConsumedMin.Should().Be(1);
        row.ConsumedMax.Should().Be(4);
        row.ConsumedAvg.Should().Be(2.5);
        row.Limit.Should().Be(10);
    }

    [Fact]
    public async Task RunColdAsync_persists_nothing_within_a_bucket()
    {
        var tenantId = Guid.NewGuid();
        using var db = TestHelpers.NewInMemoryDb($"usage-nobucket-{Guid.NewGuid():N}");
        await SeedActiveTenantWithInfobase(db, tenantId, limit: 10);

        var accumulator = new StubAccumulator([]); // внутри бакета — нечего флашить

        var job = NewJob(db, accumulator);
        await job.RunColdAsync(CancellationToken.None);

        accumulator.LastSamples.Should().ContainSingle("семпл подаётся каждый цикл");
        db.LicenseUsageSnapshots.Should().BeEmpty("без пересечения границы бакета телеметрия не пишется");
    }

    private static ReconciliationJob NewJob(AppDbContext db, ILicenseUsageAccumulator accumulator)
    {
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ClusterSession>)Array.Empty<ClusterSession>());

        var settings = Substitute.For<ISettingsSnapshot>(); // GetInt → null → дефолты
        var enforcer = Substitute.For<IKillEnforcer>();

        return new ReconciliationJob(
            cluster,
            db,
            new ActiveSessionSnapshotStore(),
            new HotTierRegistry(),
            enforcer,
            new EnforcementGate(),
            settings,
            accumulator,
            new LicenseFactCache(),
            TestHelpers.FixedClock(Now),
            TestMetrics.Reconciliation(),
            NullLogger<ReconciliationJob>.Instance);
    }

    private static async Task SeedActiveTenantWithInfobase(AppDbContext db, Guid tenantId, int limit)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            MaxConcurrentLicenses = limit,
            IsActive = true,
            CreatedAt = Now,
        });
        db.Infobases.Add(new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "БП",
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseName = "db",
            Status = InfobaseStatus.Active,
            CreatedAt = Now,
        });
        await db.SaveChangesAsync();
    }

    private sealed class StubAccumulator : ILicenseUsageAccumulator
    {
        private readonly IReadOnlyList<LicenseUsageBucket> _toReturn;
        public StubAccumulator(IReadOnlyList<LicenseUsageBucket> toReturn) => _toReturn = toReturn;

        public IReadOnlyCollection<LicenseUsageSample>? LastSamples { get; private set; }

        public IReadOnlyList<LicenseUsageBucket> RecordSample(
            DateTime sampleUtc, IReadOnlyCollection<LicenseUsageSample> samples)
        {
            LastSamples = samples;
            return _toReturn;
        }
    }
}
