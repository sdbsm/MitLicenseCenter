using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

public sealed class KillEnforcerCapTests
{
    [Fact]
    public async Task Stops_at_20_kills_even_when_more_are_over_limit()
    {
        // Arrange: tenant with limit=1, 31 sessions → 30 over-limit, but cap=20.
        var tenantId = Guid.NewGuid();
        var clusterInfobaseId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);

        var sessions = Enumerable.Range(0, 31).Select(i => new SnapshotSessionEntry(
            SessionId: Guid.NewGuid(),
            ClusterInfobaseId: clusterInfobaseId,
            TenantId: tenantId,
            TenantName: "Acme",
            InfobaseName: "БП",
            AppId: "1CV8C",
            UserName: $"user{i}",
            Host: "WS01",
            ConsumesLicense: true,
            StartedAtUtc: baseTime.AddSeconds(i))).ToList();

        var payload = new SnapshotPayload(sessions, baseTime.AddMinutes(10), 42, "Rest");

        var freshSessions = sessions.Select(s => new ClusterSession(
            s.SessionId, s.ClusterInfobaseId, s.AppId, s.UserName, s.Host,
            s.ConsumesLicense, s.StartedAtUtc)).ToList();

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>()).Returns(freshSessions);
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new KillSessionResult(Killed: true, AlreadyGone: false));

        var audit = new TestHelpers.CapturingAuditLogger();

        using var db = TestHelpers.NewInMemoryDb();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            MaxConcurrentLicenses = 1,
            IsActive = true,
            CreatedAt = baseTime,
        });
        await db.SaveChangesAsync();

        var enforcer = new KillEnforcer(cluster, audit, db, NullLogger<KillEnforcer>.Instance);

        // Act
        await enforcer.EnforceAsync(payload, CancellationToken.None);

        // Assert: exactly 20 kills (cap), not 30.
        await cluster.Received(20).KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>());
        audit.Entries.Should().HaveCount(20);
    }
}
