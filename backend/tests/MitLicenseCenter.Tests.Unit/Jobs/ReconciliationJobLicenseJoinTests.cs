using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Reporting;
using MitLicenseCenter.Application.Sessions;
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

// ADR-48 (MLC-166): холодный тир сшивает сеансы с фактом `rac --licenses` по SessionId.
// Сеанс в licensed-set → Consuming; известен факту, но не в set → NotConsuming; факт
// недоступен (null) → все Pending + LicenseFactAvailable=false. Тесты на SQLite (не
// InMemory) — холодный цикл читает Infobase/Tenant (RowVersion), MLC-163.
public sealed class ReconciliationJobLicenseJoinTests
{
    private static readonly DateTime Now = new(2026, 6, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Cold_join_classifies_consuming_notconsuming_by_licensed_set()
    {
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();

        var licensedId = Guid.NewGuid();   // в licensed-set → Consuming
        var unlicensedId = Guid.NewGuid(); // известен, не в set → NotConsuming

        var sessions = new[]
        {
            Session(licensedId, clusterInfobaseId),
            Session(unlicensedId, clusterInfobaseId),
        };

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ClusterSession>)sessions);
        cluster.ListLicensedSessionIdsAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlySet<Guid>?>(new HashSet<Guid> { licensedId });

        var (store, cache) = await RunColdAsync(cluster, tenantId, clusterInfobaseId, limit: 10);

        var payload = store.Current();
        payload.LicenseFactAvailable.Should().BeTrue();
        payload.Items.Single(e => e.SessionId == licensedId).LicenseStatus
            .Should().Be(LicenseStatus.Consuming);
        payload.Items.Single(e => e.SessionId == unlicensedId).LicenseStatus
            .Should().Be(LicenseStatus.NotConsuming);

        // Кэш факта обновлён для горячего тира: оба сеанса известны.
        var fact = cache.Current();
        fact.Available.Should().BeTrue();
        fact.Classify(licensedId).Should().Be(LicenseStatus.Consuming);
        fact.Classify(unlicensedId).Should().Be(LicenseStatus.NotConsuming);
    }

    [Fact]
    public async Task Cold_join_marks_all_pending_when_fact_unavailable()
    {
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();
        var sid = Guid.NewGuid();

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ClusterSession>)new[] { Session(sid, clusterInfobaseId) });
        cluster.ListLicensedSessionIdsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<Guid>?)null); // факт недоступен

        var (store, cache) = await RunColdAsync(cluster, tenantId, clusterInfobaseId, limit: 10);

        var payload = store.Current();
        // Снимок строится для UI, но факт помечен недоступным и сеанс — Pending.
        payload.LicenseFactAvailable.Should().BeFalse();
        payload.Items.Single().LicenseStatus.Should().Be(LicenseStatus.Pending);

        // Кэш факта: недоступен; сеанс неизвестен → Pending для горячего тира.
        var fact = cache.Current();
        fact.Available.Should().BeFalse();
        fact.Classify(sid).Should().Be(LicenseStatus.Pending);
    }

    private static ClusterSession Session(Guid sessionId, Guid clusterInfobaseId)
        => new(sessionId, clusterInfobaseId, "1CV8C", "user", "WS01", Now.AddMinutes(-5));

    private static async Task<(IActiveSessionSnapshotStore store, ILicenseFactCache cache)> RunColdAsync(
        IClusterClient cluster, Guid tenantId, Guid clusterInfobaseId, int limit)
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        await using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Acme",
                MaxConcurrentLicenses = limit,
                IsActive = true,
                CreatedAt = Now,
            });
            seed.Infobases.Add(new Infobase
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "БП",
                ClusterInfobaseId = clusterInfobaseId,
                DatabaseName = "db",
                Status = InfobaseStatus.Active,
                CreatedAt = Now,
            });
            await seed.SaveChangesAsync();
        }

        var store = new ActiveSessionSnapshotStore();
        var cache = new LicenseFactCache();
        var settings = Substitute.For<ISettingsSnapshot>();
        var enforcer = Substitute.For<IKillEnforcer>();

        await using var db = sqlite.NewContext();
        var job = new ReconciliationJob(
            cluster, db, store, new HotTierRegistry(), enforcer, new EnforcementGate(),
            settings, new StubAccumulator(), cache, TestHelpers.FixedClock(Now),
            TestMetrics.Reconciliation(), NullLogger<ReconciliationJob>.Instance);

        await job.RunColdAsync(CancellationToken.None);
        return (store, cache);
    }

    private sealed class StubAccumulator : ILicenseUsageAccumulator
    {
        public IReadOnlyList<LicenseUsageBucket> RecordSample(
            DateTime sampleUtc, IReadOnlyCollection<LicenseUsageSample> samples)
            => [];
    }
}
