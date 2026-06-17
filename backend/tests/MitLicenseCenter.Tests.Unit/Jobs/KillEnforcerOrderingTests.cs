using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

public sealed class KillEnforcerOrderingTests
{
    [Fact]
    public async Task Kills_newest_sessions_first_and_leaves_oldest_alive()
    {
        // Arrange: tenant with limit=3, 5 sessions consuming licenses.
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);

        var sessions = Enumerable.Range(0, 5).Select(i => new SnapshotSessionEntry(
            SessionId: Guid.NewGuid(),
            ClusterInfobaseId: clusterInfobaseId,
            TenantId: tenantId,
            TenantName: "Acme",
            InfobaseName: "БП",
            AppId: "1CV8C",
            UserName: $"user{i}",
            Host: "WS01",
            LicenseStatus: LicenseStatus.Consuming,
            StartedAtUtc: baseTime.AddMinutes(i))).ToList();

        var payload = new SnapshotPayload(sessions, baseTime.AddMinutes(10), 42, "Rest", LicenseFactAvailable: true);

        // Fresh re-fetch returns same sessions as ClusterSession.
        var freshSessions = sessions.Select(s => new ClusterSession(
            s.SessionId, s.ClusterInfobaseId, s.AppId, s.UserName, s.Host, s.StartedAtUtc))
            .ToList();

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>()).Returns(freshSessions);
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(new KillSessionResult(Killed: true, AlreadyGone: false));

        var audit = new TestHelpers.CapturingAuditLogger();

        using var db = TestHelpers.NewInMemoryDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            MaxConcurrentLicenses = 3,
            IsActive = true,
            CreatedAt = baseTime,
        });
        await db.SaveChangesAsync();

        var enforcer = new KillEnforcer(cluster, audit, db, Substitute.For<ISettingsSnapshot>(), TestHelpers.FixedClock(baseTime.AddMinutes(10)), TestMetrics.Reconciliation(), NullLogger<KillEnforcer>.Instance);

        // Act
        await enforcer.EnforceAsync(payload, freshSessions: null, CancellationToken.None);

        // Assert: 2 newest killed (index 4 and 3), 3 oldest survive.
        await cluster.Received(2).KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>(), Arg.Any<string?>());

        var killedIds = cluster.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IClusterClient.KillSessionAsync))
            .Select(c => ((SessionDescriptor)c.GetArguments()[0]!).SessionId)
            .ToList();

        killedIds.Should().HaveCount(2);
        killedIds[0].Should().Be(sessions[4].SessionId, "newest killed first");
        killedIds[1].Should().Be(sessions[3].SessionId, "second newest killed second");

        audit.Entries.Should().HaveCount(2);
        audit.Entries.Should().AllSatisfy(e =>
        {
            e.Action.Should().Be(AuditActionType.SessionKilled);
            e.Reason.Should().Be(AuditReason.LimitExceeded);
            e.TenantId.Should().Be(tenantId);
        });
    }
}
