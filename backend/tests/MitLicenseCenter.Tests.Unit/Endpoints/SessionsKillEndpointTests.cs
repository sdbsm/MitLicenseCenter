using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

public sealed class SessionsKillEndpointTests
{
    private static SnapshotSessionEntry MakeSession(Guid? sessionId = null)
    {
        return new SnapshotSessionEntry(
            SessionId: sessionId ?? Guid.NewGuid(),
            ClusterInfobaseId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            TenantName: "Acme",
            InfobaseName: "БП",
            AppId: "1CV8C",
            UserName: "admin",
            Host: "WS01",
            ConsumesLicense: true,
            StartedAtUtc: new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Returns_NotFound_when_session_not_in_snapshot()
    {
        var store = Substitute.For<IActiveSessionSnapshotStore>();
        store.Current().Returns(new SnapshotPayload([], DateTime.UtcNow, 0, "None"));

        var cluster = Substitute.For<IClusterClient>();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await SessionsEndpoints.KillAsync(
            Guid.NewGuid(),
            new KillSessionRequest(null),
            store,
            cluster,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        await cluster.DidNotReceive().KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>());
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_NoContent_and_audits_when_session_found()
    {
        var session = MakeSession();
        var store = Substitute.For<IActiveSessionSnapshotStore>();
        store.Current().Returns(new SnapshotPayload([session], DateTime.UtcNow, 10, "Rest"));

        var cluster = Substitute.For<IClusterClient>();
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new KillSessionResult(Killed: true, AlreadyGone: false));

        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await SessionsEndpoints.KillAsync(
            session.SessionId,
            new KillSessionRequest(null),
            store,
            cluster,
            audit,
            TestHelpers.NewHttpContext("operator1"),
            CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();

        audit.Entries.Should().ContainSingle();
        var entry = audit.Entries[0];
        entry.Action.Should().Be(AuditActionType.SessionKilled);
        entry.Reason.Should().Be(AuditReason.ManualByAdmin);
        entry.TenantId.Should().Be(session.TenantId);
        entry.Initiator.Should().Be("operator1");
    }

    [Fact]
    public async Task Audit_description_includes_operator_reason_when_provided()
    {
        var session = MakeSession();
        var store = Substitute.For<IActiveSessionSnapshotStore>();
        store.Current().Returns(new SnapshotPayload([session], DateTime.UtcNow, 10, "Rest"));

        var cluster = Substitute.For<IClusterClient>();
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new KillSessionResult(Killed: true, AlreadyGone: false));

        var audit = new TestHelpers.CapturingAuditLogger();

        await SessionsEndpoints.KillAsync(
            session.SessionId,
            new KillSessionRequest("плановое обслуживание"),
            store,
            cluster,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        audit.Entries.Should().ContainSingle();
        audit.Entries[0].Description.Should().Contain("плановое обслуживание");
    }
}
