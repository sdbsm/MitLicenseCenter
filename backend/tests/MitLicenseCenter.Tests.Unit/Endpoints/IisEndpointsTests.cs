using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Publishing.Testing;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-047 (ADR-24): управление жизненным циклом IIS — discovery + recycle/start/stop
// пула, start/stop/restart сайта, iisreset. Хендлеры вызываются напрямую со
// StubIisLifecycleService (счётчики вызовов) и CapturingAuditLogger.
public sealed class IisEndpointsTests
{
    private static readonly TestHelpers.CapturingAuditLogger Discard = new();

    // ── discovery ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListApplicationPools_returns_items_available()
    {
        var iis = new StubIisLifecycleService();
        iis.Pools.Add(new IisAppPoolInfo("DefaultAppPool", IisObjectState.Started));
        iis.Pools.Add(new IisAppPoolInfo("Stopped Pool", IisObjectState.Stopped));

        var result = await IisEndpoints.ListApplicationPoolsAsync(iis, NullLoggerFactory.Instance, CancellationToken.None);

        result.Value!.Available.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Should().Be(new IisAppPoolDto("DefaultAppPool", "Started"));
    }

    [Fact]
    public async Task ListApplicationPools_discovery_failure_returns_available_false()
    {
        var iis = Substitute.For<IIisLifecycleService>();
        iis.ListApplicationPoolsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("metabase boom"));

        var result = await IisEndpoints.ListApplicationPoolsAsync(iis, NullLoggerFactory.Instance, CancellationToken.None);

        result.Value!.Available.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
        result.Value.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ListSites_returns_items_with_state()
    {
        var iis = new StubIisLifecycleService();
        iis.Sites.Add(new IisSiteStateInfo("Default Web Site", IisObjectState.Started));

        var result = await IisEndpoints.ListSitesAsync(iis, NullLoggerFactory.Instance, CancellationToken.None);

        result.Value!.Available.Should().BeTrue();
        result.Value.Items.Should().ContainSingle()
            .Which.Should().Be(new IisSiteStateDto("Default Web Site", "Started"));
    }

    [Fact]
    public async Task GetServerStatus_returns_state_available()
    {
        var iis = new StubIisLifecycleService { ServerState = IisObjectState.Started };

        var result = await IisEndpoints.GetServerStatusAsync(iis, NullLoggerFactory.Instance, CancellationToken.None);

        result.Value!.Available.Should().BeTrue();
        result.Value.State.Should().Be("Started");
    }

    [Fact]
    public async Task GetServerStatus_failure_returns_unknown_unavailable()
    {
        var iis = new StubIisLifecycleService { ServerStateThrows = new InvalidOperationException("no W3SVC") };

        var result = await IisEndpoints.GetServerStatusAsync(iis, NullLoggerFactory.Instance, CancellationToken.None);

        result.Value!.Available.Should().BeFalse();
        result.Value.State.Should().Be("Unknown");
        result.Value.Error.Should().NotBeNullOrEmpty();
    }

    // ── recycle pool (confirm-гейт) ────────────────────────────────────────────────────

    [Fact]
    public async Task RecyclePool_without_confirm_returns_409_and_does_not_call_adapter()
    {
        var iis = new StubIisLifecycleService();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.RecyclePoolAsync(
            new IisTargetRequest("DefaultAppPool", Confirm: false),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.IisConfirmRequired);
        iis.RecyclePoolCalls.Should().Be(0);
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RecyclePool_success_writes_220_audit()
    {
        var iis = new StubIisLifecycleService { ResultState = IisObjectState.Started };
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.RecyclePoolAsync(
            new IisTargetRequest("DefaultAppPool", Confirm: true),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<IisOperationResponse>>().Subject;
        ok.Value.Should().Be(new IisOperationResponse("DefaultAppPool", "Started"));
        iis.RecyclePoolCalls.Should().Be(1);
        iis.LastPoolName.Should().Be("DefaultAppPool");
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.IisApplicationPoolRecycled);
    }

    [Fact]
    public async Task RecyclePool_empty_name_returns_validation_problem()
    {
        var iis = new StubIisLifecycleService();

        var result = await IisEndpoints.RecyclePoolAsync(
            new IisTargetRequest("   ", Confirm: true),
            iis, Discard, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
        iis.RecyclePoolCalls.Should().Be(0);
    }

    [Fact]
    public async Task RecyclePool_not_found_returns_404_without_audit()
    {
        var iis = new StubIisLifecycleService { PoolOperationThrows = new KeyNotFoundException("no pool") };
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.RecyclePoolAsync(
            new IisTargetRequest("Ghost", Confirm: true),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RecyclePool_access_denied_returns_409_without_audit()
    {
        var iis = new StubIisLifecycleService { PoolOperationThrows = new UnauthorizedAccessException("denied") };
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.RecyclePoolAsync(
            new IisTargetRequest("DefaultAppPool", Confirm: true),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.IisAccessDenied);
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RecyclePool_infra_failure_returns_409_operation_failed_sanitized()
    {
        const string secret = "0x80070005 inetsrv\\config redirection.config";
        var iis = new StubIisLifecycleService { PoolOperationThrows = new IOException(secret) };
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.RecyclePoolAsync(
            new IisTargetRequest("DefaultAppPool", Confirm: true),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.IisOperationFailed);
        conflict.Value.Detail.Should().NotContain(secret);
        audit.Entries.Should().BeEmpty();
    }

    // ── pool start/stop ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartPool_success_writes_221_audit_no_confirm_required()
    {
        var iis = new StubIisLifecycleService { ResultState = IisObjectState.Started };
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.StartPoolAsync(
            new IisTargetRequest("DefaultAppPool"),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<IisOperationResponse>>();
        iis.StartPoolCalls.Should().Be(1);
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.IisApplicationPoolStarted);
    }

    [Fact]
    public async Task StopPool_success_writes_222_audit()
    {
        var iis = new StubIisLifecycleService { ResultState = IisObjectState.Stopped };
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.StopPoolAsync(
            new IisTargetRequest("DefaultAppPool"),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<IisOperationResponse>>();
        iis.StopPoolCalls.Should().Be(1);
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.IisApplicationPoolStopped);
    }

    // ── site start/stop/restart ────────────────────────────────────────────────────────

    [Fact]
    public async Task StartSite_success_writes_223_audit()
    {
        var iis = new StubIisLifecycleService();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.StartSiteAsync(
            new IisTargetRequest("Default Web Site"),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<IisOperationResponse>>();
        iis.StartSiteCalls.Should().Be(1);
        iis.LastSiteName.Should().Be("Default Web Site");
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.IisSiteStarted);
    }

    [Fact]
    public async Task StopSite_success_writes_224_audit()
    {
        var iis = new StubIisLifecycleService { ResultState = IisObjectState.Stopped };
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.StopSiteAsync(
            new IisTargetRequest("Default Web Site"),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<IisOperationResponse>>();
        iis.StopSiteCalls.Should().Be(1);
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.IisSiteStopped);
    }

    [Fact]
    public async Task RestartSite_success_writes_225_audit()
    {
        var iis = new StubIisLifecycleService();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.RestartSiteAsync(
            new IisTargetRequest("Default Web Site"),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<IisOperationResponse>>();
        iis.RestartSiteCalls.Should().Be(1);
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.IisSiteRestarted);
    }

    [Fact]
    public async Task Site_not_found_returns_404()
    {
        var iis = new StubIisLifecycleService { SiteOperationThrows = new KeyNotFoundException("no site") };

        var result = await IisEndpoints.RestartSiteAsync(
            new IisTargetRequest("Ghost"),
            iis, Discard, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    // ── iisreset ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetIis_without_confirm_returns_409_and_does_not_call_adapter()
    {
        var iis = new StubIisLifecycleService();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.ResetIisAsync(
            new IisResetRequest(Confirm: false),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.IisConfirmRequired);
        iis.RestartIisCalls.Should().Be(0);
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetIis_success_writes_226_audit()
    {
        var iis = new StubIisLifecycleService();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.ResetIisAsync(
            new IisResetRequest(Confirm: true),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        iis.RestartIisCalls.Should().Be(1);
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.IisReset);
    }

    [Fact]
    public async Task ResetIis_failure_returns_409_without_audit()
    {
        var iis = new StubIisLifecycleService { ServerOperationThrows = new InvalidOperationException("iisreset exit=1") };
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.ResetIisAsync(
            new IisResetRequest(Confirm: true),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.IisOperationFailed);
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task StopIis_without_confirm_returns_409_and_does_not_call_adapter()
    {
        var iis = new StubIisLifecycleService();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.StopIisAsync(
            new IisResetRequest(Confirm: false),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.IisConfirmRequired);
        iis.StopIisCalls.Should().Be(0);
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task StopIis_success_writes_227_audit()
    {
        var iis = new StubIisLifecycleService();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.StopIisAsync(
            new IisResetRequest(Confirm: true),
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        iis.StopIisCalls.Should().Be(1);
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.IisStopped);
    }

    [Fact]
    public async Task StartIis_success_writes_228_audit_no_confirm_required()
    {
        var iis = new StubIisLifecycleService();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await IisEndpoints.StartIisAsync(
            iis, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        iis.StartIisCalls.Should().Be(1);
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.IisStarted);
    }
}
