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
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-044: cold (Hangfire ReconciliationJob) и hot (HotTierPollingService) теперь ОБА
// enforce'ят. Общий IEnforcementGate обязан исключить over-kill (MLC-001) при их
// одновременном запуске: убито ровно (Consumed - Limit), без двойного аудита и без
// повторных kill-вызовов — второй вошедший видит kills первого через свежий re-fetch.
public sealed class HotColdEnforcementOverKillTests
{
    private const int Limit = 5;
    private const int Over = 4;            // K: на сколько превышен лимит
    private const int Total = Limit + Over;

    [Fact]
    public async Task Concurrent_cold_and_hot_enforcement_does_not_over_kill()
    {
        var baseTime = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();
        const string tenantName = "Acme";
        const string infobaseName = "БП";

        // Total потребляющих сессий на одной инфобазе (over на Over).
        var sessions = Enumerable.Range(0, Total).Select(i => new ClusterSession(
            SessionId: Guid.NewGuid(),
            ClusterInfobaseId: clusterInfobaseId,
            AppId: "1CV8C",
            UserName: $"user{i}",
            Host: "WS01",
            ConsumesLicense: true,
            StartedAtUtc: baseTime.AddSeconds(i))).ToList();

        // Stateful-кластер: kill реально удаляет сессию; повторный kill уже убитой →
        // AlreadyGone. Считает реальные удаления и число kill-вызовов.
        var cluster = new StatefulFakeClusterClient(sessions);
        var audit = new ConcurrentAuditLogger();

        var registry = new HotTierRegistry();
        registry.Promote(tenantId);                       // tenant уже в hot-тире (at-risk)

        var store = new ActiveSessionSnapshotStore();
        var gate = new EnforcementGate();
        var clock = TestHelpers.FixedClock(baseTime.AddMinutes(10));
        var metrics = TestMetrics.Reconciliation(registry);

        var settings = Substitute.For<ISettingsSnapshot>();  // GetInt → null → дефолты (cold 25с, threshold 90%)

        // Денормализация cold-снимка и лимиты enforcer'ов — read-only в enforcement.
        // Cold и hot работают в разных потоках → разные изолированные InMemory-БД
        // (контексты с одним именем разделяют стор, поэтому имена разные). Внутри cold
        // DbContext используется последовательно (await-цепочка), поэтому ReconciliationJob
        // и cold-enforcer делят один контекст безопасно.
        using var coldDb = TestHelpers.NewInMemoryDb($"overkill-cold-{Guid.NewGuid():N}");
        using var hotDb = TestHelpers.NewInMemoryDb($"overkill-hot-{Guid.NewGuid():N}");
        await SeedAsync(coldDb, tenantId, tenantName, clusterInfobaseId, infobaseName, baseTime);
        await SeedAsync(hotDb, tenantId, tenantName, clusterInfobaseId, infobaseName, baseTime);

        // Пред-засев снимка: tenant уже hot → cold промоутил его прошлым циклом, маппинг
        // IB→tenant в снимке есть (hot строит денормализацию по текущему снимку).
        var seededEntries = sessions.Select(s => Entry(s, tenantId, tenantName, infobaseName)).ToList();
        store.Replace(new SnapshotPayload(seededEntries, baseTime, 0, "Ras"));

        var coldEnforcer = new KillEnforcer(cluster, audit, coldDb, settings, clock, metrics, NullLogger<KillEnforcer>.Instance);
        var hotEnforcer = new KillEnforcer(cluster, audit, hotDb, settings, clock, metrics, NullLogger<KillEnforcer>.Instance);

        var cold = new ReconciliationJob(
            cluster, coldDb, store, registry, coldEnforcer, gate, settings,
            new ColdThrottleState(), new LicenseUsageAccumulator(), clock, metrics,
            NullLogger<ReconciliationJob>.Instance);

        var hot = new HotTierPollingService(
            Substitute.For<IServiceScopeFactory>(), store, registry, gate, settings,
            clock, metrics, NullLogger<HotTierPollingService>.Instance);

        // Act: cold и hot enforce одновременно.
        await Task.WhenAll(
            cold.RunColdAsync(CancellationToken.None),
            hot.RunCycleOnceAsync(cluster, hotEnforcer, CancellationToken.None));

        // Assert: убито ровно Over, осталось Limit живых; без двойного аудита и без лишних
        // kill-вызовов (второй путь, увидев Consumed==Limit, не делает ни одного terminate).
        cluster.LiveCount.Should().Be(Limit, "kill ровно (Consumed - Limit), не больше");
        cluster.ActualKills.Should().Be(Over);
        cluster.TerminateCalls.Should().Be(Over, "второй вошедший видит at-limit и не вызывает terminate");
        audit.Entries.Should().HaveCount(Over, "общий замок исключает двойной аудит/over-kill");
        audit.Entries.Should().OnlyContain(e =>
            e.Action == AuditActionType.SessionKilled
            && e.Reason == AuditReason.LimitExceeded
            && e.Initiator == "System");
    }

    private static async Task SeedAsync(
        AppDbContext db, Guid tenantId, string tenantName,
        Guid clusterInfobaseId, string infobaseName, DateTime now)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = tenantName,
            MaxConcurrentLicenses = Limit,
            IsActive = true,
            CreatedAt = now,
        });
        db.Infobases.Add(new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = infobaseName,
            ClusterInfobaseId = clusterInfobaseId,
            DatabaseServer = "sql",
            DatabaseName = "db",
            CreatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private static SnapshotSessionEntry Entry(
        ClusterSession s, Guid tenantId, string tenantName, string infobaseName)
        => new(
            s.SessionId, s.ClusterInfobaseId, tenantId, tenantName, infobaseName,
            s.AppId, s.UserName, s.Host, s.ConsumesLicense, s.StartedAtUtc);

    // Потокобезопасный кластер с реальным состоянием: kill удаляет сессию, повторный
    // kill уже убитой → AlreadyGone. Достаточно для конкурентного cold+hot enforcement.
    private sealed class StatefulFakeClusterClient(IEnumerable<ClusterSession> initial) : IClusterClient
    {
        private readonly object _gate = new();
        private readonly Dictionary<Guid, ClusterSession> _sessions = initial.ToDictionary(s => s.SessionId);
        private int _actualKills;
        private int _terminateCalls;

        public int ActualKills => Volatile.Read(ref _actualKills);
        public int TerminateCalls => Volatile.Read(ref _terminateCalls);
        public int LiveCount { get { lock (_gate) return _sessions.Count; } }

        public Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct)
        {
            lock (_gate)
                return Task.FromResult<IReadOnlyList<ClusterSession>>(_sessions.Values.ToList());
        }

        public Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct)
        {
            Interlocked.Increment(ref _terminateCalls);
            lock (_gate)
            {
                if (_sessions.TryGetValue(descriptor.SessionId, out var s)
                    && s.ClusterInfobaseId == descriptor.ClusterInfobaseId
                    && s.AppId == descriptor.AppId
                    && s.StartedAtUtc == descriptor.StartedAtUtc)
                {
                    _sessions.Remove(descriptor.SessionId);
                    Interlocked.Increment(ref _actualKills);
                    return Task.FromResult(new KillSessionResult(Killed: true, AlreadyGone: false));
                }

                return Task.FromResult(new KillSessionResult(Killed: false, AlreadyGone: true));
            }
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

    private sealed class ConcurrentAuditLogger : IAuditLogger
    {
        private readonly object _gate = new();
        private readonly List<(AuditActionType Action, string Initiator, string Description, Guid? TenantId, AuditReason? Reason)> _entries = [];

        public IReadOnlyList<(AuditActionType Action, string Initiator, string Description, Guid? TenantId, AuditReason? Reason)> Entries
        {
            get { lock (_gate) return _entries.ToList(); }
        }

        public Task LogAsync(
            AuditActionType action,
            string initiator,
            string description,
            Guid? tenantId = null,
            AuditReason? reason = null,
            CancellationToken ct = default)
        {
            lock (_gate)
                _entries.Add((action, initiator, description, tenantId, reason));
            return Task.CompletedTask;
        }
    }
}
