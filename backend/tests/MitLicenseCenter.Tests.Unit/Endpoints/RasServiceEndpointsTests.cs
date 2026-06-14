using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// Эндпоинты управления службой RAS (MLC-159, ADR-47): диагностика + register/update/start.
// Хендлеры вызываются напрямую с мок-IRasServiceManager и CapturingAuditLogger.
// Авторизация Admin задаётся декларативно (RequireAuthorization(Roles.Admin)) и проверяется
// маршрутным слоем — здесь проверяем контракт ответов, аудит-серию 600 и 409 на сбое.
public sealed class RasServiceEndpointsTests
{
    // ── статус ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_returns_diagnosis_with_state_and_preview()
    {
        var manager = Substitute.For<IRasServiceManager>();
        manager.DiagnoseAsync(Arg.Any<CancellationToken>()).Returns(new RasServiceDiagnosis(
            State: RasServiceState.NotRegistered,
            Service: null,
            Target: new RasServiceTarget("C:\\ras.exe", "8.5.1.1302", "1545", "localhost:1540"),
            CommandPreview: "sc create MitLicenseRas binPath= \"...\" start= auto",
            TargetReady: true,
            Issue: null));

        var result = await RasServiceEndpoints.GetStatusAsync(
            manager, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<RasServiceStatusResponse>>().Subject;
        ok.Value!.State.Should().Be("NotRegistered");
        ok.Value.Target!.PlatformVersion.Should().Be("8.5.1.1302");
        ok.Value.CommandPreview.Should().Contain("sc create");
    }

    [Fact]
    public async Task GetStatus_maps_operation_exception_to_409()
    {
        var manager = Substitute.For<IRasServiceManager>();
        manager.DiagnoseAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new RasServiceOperationException("Не удалось получить список служб Windows (sc query)."));

        var result = await RasServiceEndpoints.GetStatusAsync(
            manager, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.RasServiceOperationFailed);
    }

    // ── register / update / start: аудит 600-серии ──────────────────────────────────────

    [Fact]
    public async Task Register_success_writes_600_audit()
    {
        var manager = Substitute.For<IRasServiceManager>();
        manager.RegisterAsync(Arg.Any<CancellationToken>())
            .Returns(new RasServiceOperationResult(RasServiceState.Ok, "MitLicenseRas", "8.5.1.1302", "1545"));
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await RasServiceEndpoints.RegisterAsync(
            manager, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RasServiceOperationResponse>>();
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.RasServiceRegistered);
        // server-scope: TenantId не пишется; секреты в описание не попадают.
        var entry = audit.Entries.Single();
        entry.TenantId.Should().BeNull();
        entry.Description.Should().Contain("8.5.1.1302").And.Contain("1545");
        entry.Description.Should().NotContain("password");
    }

    [Fact]
    public async Task Update_success_writes_601_audit()
    {
        var manager = Substitute.For<IRasServiceManager>();
        manager.UpdateAsync(Arg.Any<CancellationToken>())
            .Returns(new RasServiceOperationResult(RasServiceState.Ok, "MitLicenseRas", "8.5.1.1302", "1545"));
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await RasServiceEndpoints.UpdateAsync(
            manager, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RasServiceOperationResponse>>();
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.RasServiceUpdated);
    }

    [Fact]
    public async Task Start_success_writes_602_audit()
    {
        var manager = Substitute.For<IRasServiceManager>();
        manager.StartAsync(Arg.Any<CancellationToken>())
            .Returns(new RasServiceOperationResult(RasServiceState.Ok, "MitLicenseRas", null, null));
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await RasServiceEndpoints.StartAsync(
            manager, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<RasServiceOperationResponse>>();
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.RasServiceStarted);
    }

    [Fact]
    public async Task Register_failure_returns_409_without_audit()
    {
        var manager = Substitute.For<IRasServiceManager>();
        manager.RegisterAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new RasServiceOperationException("Не найден ras.exe платформы 8.5.1.1302."));
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await RasServiceEndpoints.RegisterAsync(
            manager, audit, TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.RasServiceOperationFailed);
        audit.Entries.Should().BeEmpty();
    }
}
