using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// ADR-48 (MLC-166): enforcement опирается на факт `rac --licenses`. Тесты на SQLite
// (не InMemory — прецедент MLC-163: InMemory маскирует concurrency/таргетные UPDATE,
// а enforcement затрагивает Tenant с RowVersion).
public sealed class KillEnforcerLicenseFactTests
{
    private static readonly DateTime ClockNow = new(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc);

    // Факт недоступен (LicenseFactAvailable=false) ⇒ KillEnforcer не завершает ни одного
    // сеанса, даже при явном превышении лимита. «Не рубить вслепую».
    [Fact]
    public async Task Does_not_kill_when_license_fact_unavailable()
    {
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();

        // 3 потребляющих сеанса при лимите 1 → over by 2, но факт недоступен.
        var sessions = Enumerable.Range(0, 3).Select(i => new SnapshotSessionEntry(
            Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
            "1CV8C", $"user{i}", "WS01", LicenseStatus.Consuming,
            ClockNow.AddMinutes(-30))).ToList();

        var payload = new SnapshotPayload(sessions, ClockNow, 10, "Ras", LicenseFactAvailable: false);

        var (cluster, audit) = await RunAsync(tenantId, limit: 1, payload);

        await cluster.DidNotReceive().KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>());
        audit.Entries.Should().BeEmpty();
        // Факт недоступен → re-fetch вообще не нужен (ранний выход до него).
        await cluster.DidNotReceive().ListActiveSessionsAsync(Arg.Any<CancellationToken>());
    }

    // Факт доступен ⇒ over-limit сеансы завершаются (контрольный сценарий к паузе выше).
    [Fact]
    public async Task Kills_when_license_fact_available()
    {
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();

        var sessions = Enumerable.Range(0, 3).Select(i => new SnapshotSessionEntry(
            Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
            "1CV8C", $"user{i}", "WS01", LicenseStatus.Consuming,
            ClockNow.AddMinutes(-30))).ToList();

        var payload = new SnapshotPayload(sessions, ClockNow, 10, "Ras", LicenseFactAvailable: true);

        var (cluster, audit) = await RunAsync(tenantId, limit: 1, payload);

        // over by 2 → ровно 2 kill.
        await cluster.Received(2).KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>());
        audit.Entries.Should().HaveCount(2);
    }

    // Инвариант «grace ≥ холодного интервала»: оператор задал KillGrace=5, но холодный
    // интервал=60 → effectiveGrace=60. Сеанс возрастом 30с (> 5, но < 60) НЕ завершается.
    [Fact]
    public async Task Effective_grace_is_at_least_cold_interval()
    {
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();

        var sessions = new[]
        {
            new SnapshotSessionEntry(Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
                "1CV8C", "user-old", "WS01", LicenseStatus.Consuming, ClockNow.AddMinutes(-30)),
            new SnapshotSessionEntry(Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
                "1CV8C", "user-young", "WS01", LicenseStatus.Consuming, ClockNow.AddSeconds(-30)),
        };

        var payload = new SnapshotPayload(sessions, ClockNow, 10, "Ras", LicenseFactAvailable: true);

        // KillGrace=5 (мин допустимый), ColdInterval=60. Newest-first целит в молодой
        // (30с) сеанс: 30с < effectiveGrace(60) → break, у тенанта вообще не убиваем.
        var (cluster, audit) = await RunAsync(
            tenantId, limit: 1, payload, killGraceSeconds: 5, coldIntervalSeconds: 60);

        await cluster.DidNotReceive().KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>());
        audit.Entries.Should().BeEmpty();
    }

    private static async Task<(IClusterClient cluster, TestHelpers.CapturingAuditLogger audit)> RunAsync(
        Guid tenantId,
        int limit,
        SnapshotPayload payload,
        int? killGraceSeconds = null,
        int? coldIntervalSeconds = null)
    {
        var freshSessions = payload.Items.Select(s => new ClusterSession(
            s.SessionId, s.ClusterInfobaseId, s.AppId, s.UserName, s.Host, s.StartedAtUtc)).ToList();

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>()).Returns(freshSessions);
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new KillSessionResult(Killed: true, AlreadyGone: false));

        var audit = new TestHelpers.CapturingAuditLogger();

        // SQLite (не InMemory): enforcement читает Tenant (с RowVersion) — InMemory
        // маскирует concurrency-семантику (MLC-163).
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        await using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Acme",
                MaxConcurrentLicenses = limit,
                IsActive = true,
                CreatedAt = ClockNow,
            });
            await seed.SaveChangesAsync();
        }

        var settings = Substitute.For<ISettingsSnapshot>();
        if (killGraceSeconds is not null)
            settings.GetInt(SettingKey.EnforcementKillGraceSeconds).Returns(killGraceSeconds);
        if (coldIntervalSeconds is not null)
            settings.GetInt(SettingKey.PollingColdIntervalSeconds).Returns(coldIntervalSeconds);

        await using var db = sqlite.NewContext();
        var enforcer = new KillEnforcer(
            cluster, audit, db, settings, TestHelpers.FixedClock(ClockNow),
            TestMetrics.Reconciliation(), NullLogger<KillEnforcer>.Instance);

        await enforcer.EnforceAsync(payload, freshSessions: null, CancellationToken.None);

        return (cluster, audit);
    }
}
