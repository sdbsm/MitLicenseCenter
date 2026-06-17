using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

public sealed class KillEnforcerStaleCandidateTests
{
    [Fact]
    public async Task Skips_candidate_when_refetch_returns_different_StartedAt()
    {
        // Arrange: 2 sessions, limit=1 → 1 kill needed.
        // Re-fetch returns same SessionId but different StartedAtUtc → stale, skip.
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();
        var now = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);

        var sessions = new[]
        {
            new SnapshotSessionEntry(Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
                "1CV8C", "user1", "WS01", LicenseStatus.Consuming, now),
            new SnapshotSessionEntry(Guid.NewGuid(), clusterInfobaseId, tenantId, "Acme", "БП",
                "1CV8C", "user2", "WS01", LicenseStatus.Consuming, now.AddMinutes(5)),
        };

        var payload = new SnapshotPayload(sessions, now.AddMinutes(10), 10, "Rest", LicenseFactAvailable: true);

        // Re-fetch: all sessions have a different StartedAtUtc (simulating restart).
        var freshSessions = sessions.Select(s => new ClusterSession(
            s.SessionId, s.ClusterInfobaseId, s.AppId, s.UserName, s.Host,
            s.StartedAtUtc.AddHours(1))).ToList();

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>()).Returns(freshSessions);

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

        var enforcer = new KillEnforcer(cluster, audit, db, Substitute.For<ISettingsSnapshot>(), TestHelpers.FixedClock(now.AddMinutes(10)), TestMetrics.Reconciliation(), NullLogger<KillEnforcer>.Instance);

        // Act
        await enforcer.EnforceAsync(payload, freshSessions: null, CancellationToken.None);

        // Assert: no KillSessionAsync calls (all candidates stale), no audit.
        await cluster.DidNotReceive().KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>(), Arg.Any<string?>());
        audit.Entries.Should().BeEmpty();
    }
}
