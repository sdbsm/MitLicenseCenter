using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
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
            LicenseStatus: LicenseStatus.Consuming,
            StartedAtUtc: new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Returns_NotFound_when_session_not_in_snapshot()
    {
        var store = Substitute.For<IActiveSessionSnapshotStore>();
        store.Current().Returns(new SnapshotPayload([], DateTime.UtcNow, 0, "None", LicenseFactAvailable: true));

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
        store.Current().Returns(new SnapshotPayload([session], DateTime.UtcNow, 10, "Rest", LicenseFactAvailable: true));

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ClusterSession>());
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
        store.Current().Returns(new SnapshotPayload([session], DateTime.UtcNow, 10, "Rest", LicenseFactAvailable: true));

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ClusterSession>());
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

    [Fact]
    public async Task Audits_and_returns_NoContent_when_session_already_gone()
    {
        // Идемпотентный успех: rac сообщил, что сеанс уже отсутствует (AlreadyGone).
        // Это считается завершением — аудит пишется (как в KillEnforcer).
        var session = MakeSession();
        var store = Substitute.For<IActiveSessionSnapshotStore>();
        store.Current().Returns(new SnapshotPayload([session], DateTime.UtcNow, 10, "Rest", LicenseFactAvailable: true));

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ClusterSession>());
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new KillSessionResult(Killed: false, AlreadyGone: true));

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
        audit.Entries[0].Reason.Should().Be(AuditReason.ManualByAdmin);
    }

    [Fact]
    public async Task Does_not_audit_and_returns_502_when_kill_fails()
    {
        // RAS недоступен/ошибка: оба флага false. Аудит НЕ пишется (запись-ложь
        // в неизменяемом журнале недопустима), наружу — 502 CLUSTER_UNAVAILABLE.
        var session = MakeSession();
        var store = Substitute.For<IActiveSessionSnapshotStore>();
        store.Current().Returns(new SnapshotPayload([session], DateTime.UtcNow, 10, "Rest", LicenseFactAvailable: true));

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ClusterSession>());
        cluster.KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new KillSessionResult(Killed: false, AlreadyGone: false));

        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await SessionsEndpoints.KillAsync(
            session.SessionId,
            new KillSessionRequest(null),
            store,
            cluster,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        var problem = result.Result.Should().BeOfType<ProblemHttpResult>().Which;
        problem.ProblemDetails.Status.Should().Be(502);
        problem.ProblemDetails.Extensions["code"].Should().Be(ProblemCodes.ClusterUnavailable);
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Does_not_kill_or_audit_and_returns_409_when_descriptor_is_stale()
    {
        // Снапшот устарел: в кластере по тому же SessionId уже другой сеанс
        // (изменился StartedAt). Не убиваем чужой сеанс — 409 SESSION_STALE.
        var session = MakeSession();
        var store = Substitute.For<IActiveSessionSnapshotStore>();
        store.Current().Returns(new SnapshotPayload([session], DateTime.UtcNow, 10, "Rest", LicenseFactAvailable: true));

        var fresh = new ClusterSession(
            SessionId: session.SessionId,
            ClusterInfobaseId: session.ClusterInfobaseId,
            AppId: session.AppId,
            UserName: session.UserName,
            Host: session.Host,
            StartedAtUtc: session.StartedAtUtc.AddMinutes(30));

        var cluster = Substitute.For<IClusterClient>();
        cluster.ListActiveSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { fresh });

        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await SessionsEndpoints.KillAsync(
            session.SessionId,
            new KillSessionRequest(null),
            store,
            cluster,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Which;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.SessionStale);
        await cluster.DidNotReceive().KillSessionAsync(Arg.Any<SessionDescriptor>(), Arg.Any<CancellationToken>());
        audit.Entries.Should().BeEmpty();
    }

    // MLC-156: POST /sessions/refresh форсит cold-прогон через ColdTierPollingService и
    // отдаёт 204. Проверяем сквозь живой сервис: RefreshAsync дожидается реального прогона
    // (RunNowAsync → RunColdAsync) и возвращает NoContent без аудита.
    [Fact]
    public async Task RefreshAsync_forces_a_cold_run_and_returns_NoContent()
    {
        var runCount = 0;
        var job = Substitute.For<IReconciliationJob>();
        job.RunColdAsync(Arg.Any<CancellationToken>())
            .Returns(_ => { Interlocked.Increment(ref runCount); return Task.CompletedTask; });

        var services = new ServiceCollection();
        services.AddScoped(_ => job);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(SettingKey.PollingColdIntervalSeconds).Returns(3600);

        var cold = new ColdTierPollingService(
            scopeFactory, settings, NullLogger<ColdTierPollingService>.Instance);

        await cold.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (runCount < 1 && DateTime.UtcNow < deadline)
                await Task.Delay(10);
            var before = runCount;

            var result = await SessionsEndpoints.RefreshAsync(cold, CancellationToken.None);

            result.Should().BeOfType<NoContent>();
            runCount.Should().BeGreaterThan(before);
        }
        finally
        {
            await cold.StopAsync(CancellationToken.None);
        }
    }
}
