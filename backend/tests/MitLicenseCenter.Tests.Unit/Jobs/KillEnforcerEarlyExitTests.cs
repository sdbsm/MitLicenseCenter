using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

public sealed class KillEnforcerEarlyExitTests
{
    [Fact]
    public async Task Stops_killing_when_consumed_equals_limit()
    {
        // Arrange: tenant with limit=4, 5 sessions → only 1 kill needed.
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
            ConsumesLicense: true,
            StartedAtUtc: baseTime.AddMinutes(i))).ToList();

        var payload = new SnapshotPayload(sessions, baseTime.AddMinutes(10), 42, "Rest");

        var freshSessions = sessions.Select(s => new ClusterSession(
            s.SessionId, s.ClusterInfobaseId, s.AppId, s.UserName, s.Host, s.ConsumesLicense, s.StartedAtUtc))
            .ToList();

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
            MaxConcurrentLicenses = 4,
            IsActive = true,
            CreatedAt = baseTime,
        });
        await db.SaveChangesAsync();

        var enforcer = new KillEnforcer(cluster, audit, db, TestMetrics.Reconciliation(), NullLogger<KillEnforcer>.Instance);

        // Act
        await enforcer.EnforceAsync(payload, CancellationToken.None);

        // Assert: exactly 1 kill (consumed goes 5→4 == limit → stop).
        await cluster.Received(1).KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>());
        audit.Entries.Should().ContainSingle();
    }
}
