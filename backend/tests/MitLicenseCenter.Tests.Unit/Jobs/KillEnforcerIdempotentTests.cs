using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

public sealed class KillEnforcerIdempotentTests
{
    [Fact]
    public async Task AlreadyGone_result_does_not_throw_and_writes_single_audit_row()
    {
        // Arrange: 2 sessions, limit=1 → 1 kill needed.
        // KillSessionAsync returns (Killed:false, AlreadyGone:true).
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();
        var now = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);

        var sessions = new[]
        {
            new SnapshotSessionEntry(Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
                "1CV8C", "user1", "WS01", true, now),
            new SnapshotSessionEntry(Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
                "1CV8C", "user2", "WS01", true, now.AddMinutes(5)),
        };

        var payload = new SnapshotPayload(sessions, now.AddMinutes(10), 10, "Rest");

        var freshSessions = sessions.Select(s => new ClusterSession(
            s.SessionId, s.ClusterInfobaseId, s.AppId, s.UserName, s.Host,
            s.ConsumesLicense, s.StartedAtUtc)).ToList();

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>()).Returns(freshSessions);
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new KillSessionResult(Killed: false, AlreadyGone: true));

        var audit = new TestHelpers.CapturingAuditLogger();

        using var db = TestHelpers.NewInMemoryDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            MaxConcurrentLicenses = 1,
            IsActive = true,
            CreatedAt = now,
        });
        await db.SaveChangesAsync();

        var enforcer = new KillEnforcer(cluster, audit, db, TestMetrics.Reconciliation(), NullLogger<KillEnforcer>.Instance);

        // Act — should not throw.
        var act = () => enforcer.EnforceAsync(payload, freshSessions: null, CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Assert: 1 kill attempted, AlreadyGone treated as success, 1 audit row.
        await cluster.Received(1).KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>());
        audit.Entries.Should().ContainSingle();
        audit.Entries[0].Action.Should().Be(AuditActionType.SessionKilled);
        audit.Entries[0].Reason.Should().Be(AuditReason.LimitExceeded);
    }
}
