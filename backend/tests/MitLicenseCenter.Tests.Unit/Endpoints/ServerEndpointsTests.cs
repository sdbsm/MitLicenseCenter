using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Maintenance;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// Эндпоинты раздела «Сервер» (MLC-213, ADR-54/55): сводный статус + управление сервером 1С.
// Хендлеры вызываются напрямую с мок-IServerStatusProvider/IWindowsServiceController и
// CapturingAuditLogger. Ролевой доступ (Viewer на статус, Admin на мутации) задаётся
// декларативно и проверяется маршрутным слоем — здесь проверяем контракт, whitelist,
// Confirm-гейт, 409-маппинг и аудит-серию 800.
public sealed class ServerEndpointsTests
{
    private const string ServiceName = "1C:Enterprise 8.3.23.1865 Server Agent";

    // ── статус ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_returns_snapshot_contract()
    {
        var provider = Substitute.For<IServerStatusProvider>();
        provider.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(Snapshot(running: true));

        var result = await ServerEndpoints.GetStatusAsync(provider, CancellationToken.None);

        result.Value!.OneCServers.Should().ContainSingle();
        result.Value.Ras.State.Should().Be("Ok");
        result.Value.Sql.Instance.Should().Be("localhost");
        result.Value.Iis.State.Should().Be("Started");
        result.Value.Overall.Should().Be("Healthy");
    }

    // ── whitelist / валидация ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_unknown_service_returns_404()
    {
        var provider = Substitute.For<IServerStatusProvider>();
        provider.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(Snapshot(running: true));
        var controller = Substitute.For<IWindowsServiceController>();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await ServerEndpoints.StartOneCServerAsync(
            new OneCServerStartRequest("SomeOtherService"),
            provider, controller, audit,
            TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        audit.Entries.Should().BeEmpty();
        await controller.DidNotReceive().StartAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_empty_name_returns_validation_problem()
    {
        var provider = Substitute.For<IServerStatusProvider>();
        provider.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(Snapshot(running: true));
        var controller = Substitute.For<IWindowsServiceController>();

        var result = await ServerEndpoints.StartOneCServerAsync(
            new OneCServerStartRequest("   "),
            provider, controller, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    // ── успех: аудит 800-серии ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_success_writes_800_audit_with_null_tenant()
    {
        var provider = Substitute.For<IServerStatusProvider>();
        provider.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(Snapshot(running: false));
        var controller = Substitute.For<IWindowsServiceController>();
        controller.StartAsync(ServiceName, Arg.Any<CancellationToken>())
            .Returns(new WindowsServiceOperationResult(ServiceName, WindowsServiceStatus.Running));
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await ServerEndpoints.StartOneCServerAsync(
            new OneCServerStartRequest(ServiceName),
            provider, controller, audit,
            TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<ServerOperationResponse>>().Subject;
        ok.Value!.FinalStatus.Should().Be("Running");
        ok.Value.ServiceName.Should().Be(ServiceName);
        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActionType.OneCServerStarted);
        entry.TenantId.Should().BeNull();
        entry.Description.Should().Contain(ServiceName);
    }

    [Fact]
    public async Task Stop_success_writes_801_audit()
    {
        var provider = Substitute.For<IServerStatusProvider>();
        provider.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(Snapshot(running: true));
        var controller = Substitute.For<IWindowsServiceController>();
        controller.StopAsync(ServiceName, Arg.Any<CancellationToken>())
            .Returns(new WindowsServiceOperationResult(ServiceName, WindowsServiceStatus.Stopped));
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await ServerEndpoints.StopOneCServerAsync(
            new OneCServerStopRequest(ServiceName, Confirm: true),
            provider, controller, audit,
            TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ServerOperationResponse>>();
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.OneCServerStopped);
    }

    [Fact]
    public async Task Restart_success_writes_802_audit()
    {
        var provider = Substitute.For<IServerStatusProvider>();
        provider.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(Snapshot(running: true));
        var controller = Substitute.For<IWindowsServiceController>();
        controller.RestartAsync(ServiceName, Arg.Any<CancellationToken>())
            .Returns(new WindowsServiceOperationResult(ServiceName, WindowsServiceStatus.Running));
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await ServerEndpoints.RestartOneCServerAsync(
            new OneCServerStopRequest(ServiceName, Confirm: true),
            provider, controller, audit,
            TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<ServerOperationResponse>>();
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.OneCServerRestarted);
    }

    // ── Confirm-гейт на разрушительных операциях ──────────────────────────────────────────

    [Fact]
    public async Task Stop_without_confirm_returns_409_confirm_required()
    {
        var provider = Substitute.For<IServerStatusProvider>();
        provider.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(Snapshot(running: true));
        var controller = Substitute.For<IWindowsServiceController>();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await ServerEndpoints.StopOneCServerAsync(
            new OneCServerStopRequest(ServiceName, Confirm: false),
            provider, controller, audit,
            TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.ServerConfirmRequired);
        audit.Entries.Should().BeEmpty();
        await controller.DidNotReceive().StopAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restart_without_confirm_returns_409_confirm_required()
    {
        var provider = Substitute.For<IServerStatusProvider>();
        provider.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(Snapshot(running: true));
        var controller = Substitute.For<IWindowsServiceController>();

        var result = await ServerEndpoints.RestartOneCServerAsync(
            new OneCServerStopRequest(ServiceName, Confirm: false),
            provider, controller, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.ServerConfirmRequired);
    }

    // ── сбой операции → 409 SERVER_OPERATION_FAILED ──────────────────────────────────────

    [Fact]
    public async Task Start_operation_exception_maps_to_409_without_audit()
    {
        var provider = Substitute.For<IServerStatusProvider>();
        provider.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(Snapshot(running: false));
        var controller = Substitute.For<IWindowsServiceController>();
        controller.StartAsync(ServiceName, Arg.Any<CancellationToken>())
            .ThrowsAsync(new WindowsServiceOperationException("Служба не успела запуститься за 30 с."));
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await ServerEndpoints.StartOneCServerAsync(
            new OneCServerStartRequest(ServiceName),
            provider, controller, audit,
            TestHelpers.NewHttpContext("admin"), NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.ServerOperationFailed);
        audit.Entries.Should().BeEmpty();
    }

    // ── обслуживание: свежесть бэкапов (MLC-216) ─────────────────────────────────────────

    [Fact]
    public async Task BackupFreshness_returns_contract_with_stale_flag()
    {
        var probe = new FakeMaintenanceProbe(new BackupFreshnessSnapshot(
            MaintenanceProbeStatus.Ok,
            [
                new DatabaseBackupFreshness("acme_bp",
                    LastFullUtc: new DateTime(2026, 6, 19, 1, 0, 0, DateTimeKind.Utc),
                    LastDiffUtc: null, LastLogUtc: null, IsStale: false),
                new DatabaseBackupFreshness("stale_bp",
                    LastFullUtc: null, LastDiffUtc: null, LastLogUtc: null, IsStale: true),
            ]));

        var result = await ServerEndpoints.GetBackupFreshnessAsync(probe, CancellationToken.None);

        result.Value!.Status.Should().Be("Ok");
        result.Value.Databases.Should().HaveCount(2);
        result.Value.Databases[0].DatabaseName.Should().Be("acme_bp");
        result.Value.Databases[0].IsStale.Should().BeFalse();
        result.Value.Databases[1].IsStale.Should().BeTrue();
        result.Value.Databases[1].LastFullUtc.Should().BeNull();
    }

    [Fact]
    public async Task BackupFreshness_degrades_to_permission_denied_without_500()
    {
        // Нет прав на msdb.dbo.backupset → PermissionDenied, databases пуст, не 500.
        var probe = new FakeMaintenanceProbe(
            new BackupFreshnessSnapshot(MaintenanceProbeStatus.PermissionDenied, []));

        var result = await ServerEndpoints.GetBackupFreshnessAsync(probe, CancellationToken.None);

        result.Value!.Status.Should().Be("PermissionDenied");
        result.Value.Databases.Should().BeEmpty();
    }

    [Fact]
    public async Task BackupFreshness_degrades_to_unavailable_without_500()
    {
        var probe = new FakeMaintenanceProbe(
            new BackupFreshnessSnapshot(MaintenanceProbeStatus.Unavailable, []));

        var result = await ServerEndpoints.GetBackupFreshnessAsync(probe, CancellationToken.None);

        result.Value!.Status.Should().Be("Unavailable");
        result.Value.Databases.Should().BeEmpty();
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────

    // Ручной фейк IMaintenanceProbe (без NSubstitute) — единообразно с пробами Infrastructure.
    private sealed class FakeMaintenanceProbe(BackupFreshnessSnapshot snapshot) : IMaintenanceProbe
    {
        public Task<BackupFreshnessSnapshot> GetBackupFreshnessAsync(CancellationToken ct) =>
            Task.FromResult(snapshot);
    }

    private static ServerStatusSnapshot Snapshot(bool running) =>
        new(
            OneCServers: [new OneCServerStatus(ServiceName, running, "8.3.23.1865")],
            Ras: new RasStatusSummary("Ok", Running: true, "MitLicenseRas", Available: true, Error: null),
            Sql: new SqlStatusSummary("localhost", "MSSQLSERVER", Running: true, Available: true, Error: null),
            Iis: new IisStatusSummary("Started", Available: true, Error: null),
            Overall: ServerHealth.Healthy);
}
