using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-044 — детерминированный замер «до→после» эффекта hot-enforce, в духе
// ClusterUuidCacheSpawnMeasurementTests (MLC-041): без живой БД/процессов, на реальном
// производственном пути HotTierPollingService.RunCycleOnceAsync + KillEnforcer.EnforceAsync.
//
// Считает вызовы IClusterClient как прокси спавнов rac.exe (тёплый UUID-кэш, MLC-041:
// 1 спавн на ListActiveSessionsAsync = session.list, 1 на KillSessionAsync = session.terminate).
public sealed class HotEnforcementMeasurementTests
{
    private const int Limit = 5;
    private const int ColdIntervalSeconds = 15;  // SettingDefinitions default (MLC-154)
    private const int HotIntervalSeconds = 4;    // SettingDefinitions default

    private readonly ITestOutputHelper _out;

    public HotEnforcementMeasurementTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Hot_tick_enforces_over_limit_with_a_single_session_list_fetch()
    {
        const int over = 3;
        var (cluster, hot, enforcer) = await BuildScenarioAsync(consuming: Limit + over);

        await hot.RunCycleOnceAsync(cluster, enforcer, CancellationToken.None);

        // Латентность: kill теперь происходит на hot-тике (≤ HotIntervalSeconds), а не
        // ждёт cold-цикла (ColdIntervalSeconds). До MLC-044 hot-тик делал 0 kills.
        cluster.TerminateCalls.Should().Be(over, "hot-тик убивает превышение сам");
        // Спавн-бюджет: один session.list на тик — переиспользован и для overlay, и для
        // fresh-проверки enforcement. Никакого второго ListActiveSessionsAsync.
        cluster.ListCalls.Should().Be(1, "hot переиспользует единственный fetch (нет двойного спавна)");

        _out.WriteLine("MLC-044 — hot-enforce, сценарий over-limit (limit=5, consumed=8):");
        _out.WriteLine($"  kill latency:            before ≈ {ColdIntervalSeconds}s (cold only) → after ≤ {HotIntervalSeconds}s (hot enforces)");
        _out.WriteLine($"  session.list / hot-tick: before 1 → after {cluster.ListCalls} (переиспользован, НЕ 2)");
        _out.WriteLine($"  session.terminate / tick: before 0 → after {cluster.TerminateCalls} (транзиентно, до Consumed==Limit)");
    }

    [Fact]
    public async Task Hot_tick_at_limit_spawns_only_one_session_list_and_no_terminate()
    {
        // Steady-state: tenant hot, но Consumed == Limit (не over). EnforceAsync рано
        // выходит (overLimitTenants пуст) → 0 terminate. Hot = 1 session.list/тик.
        var (cluster, hot, enforcer) = await BuildScenarioAsync(consuming: Limit);

        await hot.RunCycleOnceAsync(cluster, enforcer, CancellationToken.None);

        cluster.ListCalls.Should().Be(1);
        cluster.TerminateCalls.Should().Be(0, "на Consumed==Limit убийств нет");

        // 1 session.list каждые HotIntervalSeconds → спавны/мин в рамках ADR-3.3 (~26/мин).
        var spawnsPerMin = 60 / HotIntervalSeconds * cluster.ListCalls;
        _out.WriteLine("MLC-044 — hot steady-state (Consumed==Limit):");
        _out.WriteLine($"  session.list / hot-tick: {cluster.ListCalls}  → ~{spawnsPerMin}/мин (≤ ~26/мин, ADR-3.3)");
        _out.WriteLine($"  session.terminate:       {cluster.TerminateCalls} (steady-state)");
        spawnsPerMin.Should().BeLessThanOrEqualTo(26);
    }

    private static async Task<(CountingClusterClient Cluster, HotTierPollingService Hot, KillEnforcer Enforcer)>
        BuildScenarioAsync(int consuming)
    {
        var baseTime = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();
        const string tenantName = "Acme";
        const string infobaseName = "БП";

        var sessions = Enumerable.Range(0, consuming).Select(i => new ClusterSession(
            Guid.NewGuid(), clusterInfobaseId, "1CV8C", $"user{i}", "WS01", true,
            baseTime.AddSeconds(i))).ToList();

        var cluster = new CountingClusterClient(sessions);
        var registry = new HotTierRegistry();
        registry.Promote(tenantId);
        var store = new ActiveSessionSnapshotStore();
        var gate = new EnforcementGate();
        var clock = TestHelpers.FixedClock(baseTime.AddMinutes(10));
        var metrics = TestMetrics.Reconciliation(registry);
        var settings = Substitute.For<ISettingsSnapshot>();

        var db = TestHelpers.NewInMemoryDb($"measure-{Guid.NewGuid():N}");
        db.Tenants.Add(new Tenant { Id = tenantId, Name = tenantName, MaxConcurrentLicenses = Limit, IsActive = true, CreatedAt = baseTime });
        db.Infobases.Add(new Infobase { Id = Guid.NewGuid(), TenantId = tenantId, Name = infobaseName, ClusterInfobaseId = clusterInfobaseId, DatabaseName = "db", CreatedAt = baseTime });
        await db.SaveChangesAsync();

        // Снимок уже знает маппинг IB→tenant (tenant hot → cold промоутил его ранее).
        var seeded = sessions.Select(s => new SnapshotSessionEntry(
            s.SessionId, s.ClusterInfobaseId, tenantId, tenantName, infobaseName,
            s.AppId, s.UserName, s.Host, s.ConsumesLicense, s.StartedAtUtc)).ToList();
        store.Replace(new SnapshotPayload(seeded, baseTime, 0, "Ras"));

        var enforcer = new KillEnforcer(cluster, new NoopAuditLogger(), db, settings, clock, metrics, NullLogger<KillEnforcer>.Instance);
        var hot = new HotTierPollingService(
            Substitute.For<IServiceScopeFactory>(), store, registry, gate, settings,
            clock, metrics, NullLogger<HotTierPollingService>.Instance);

        return (cluster, hot, enforcer);
    }

    // Stateless счётчик вызовов: список не меняется (kill «успешен», но сессия остаётся —
    // моделирует sustained over-limit для замера, как rac-stub MLC-039).
    private sealed class CountingClusterClient(IReadOnlyList<ClusterSession> sessions) : IClusterClient
    {
        private int _listCalls;
        private int _terminateCalls;

        public int ListCalls => Volatile.Read(ref _listCalls);
        public int TerminateCalls => Volatile.Read(ref _terminateCalls);

        public Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _listCalls);
            return Task.FromResult(sessions);
        }

        public Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct)
        {
            Interlocked.Increment(ref _terminateCalls);
            return Task.FromResult(new KillSessionResult(Killed: true, AlreadyGone: false));
        }

        public Task<ClusterPingResult> PingAsync(CancellationToken ct)
            => Task.FromResult(new ClusterPingResult(true, null));

        public Task<ClusterInfobaseDiscoveryResult> ListInfobasesAsync(CancellationToken ct)
            => Task.FromResult(new ClusterInfobaseDiscoveryResult([], Available: false, null));

        public Task<IReadOnlyList<OneCSessionLoad>> ListSessionLoadsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<OneCSessionLoad>>([]);

        public Task<IReadOnlyList<OneCProcessLoad>> ListProcessesAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<OneCProcessLoad>>([]);
    }

    private sealed class NoopAuditLogger : IAuditLogger
    {
        public Task LogAsync(
            AuditActionType action, string initiator, string description,
            Guid? tenantId = null, AuditReason? reason = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public void Enlist(
            AuditActionType action, string initiator, string description,
            Guid? tenantId = null, AuditReason? reason = null)
        {
        }
    }
}
