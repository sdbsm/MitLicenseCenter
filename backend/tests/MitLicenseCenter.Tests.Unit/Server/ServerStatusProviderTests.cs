using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Infrastructure.Server;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Server;

// Композиция агрегатора статуса узла (MLC-213) на моках адаптеров: нормальный агрегат,
// независимая деградация одного источника и правила Overall (Healthy/Degraded/Down/Unknown).
public sealed class ServerStatusProviderTests
{
    [Fact]
    public async Task GetStatus_aggregates_all_sources_when_healthy()
    {
        var provider = NewProvider(
            oneC: [new OneCServerStatus("1C Server 8.3", Running: true, "8.3.23.1865")],
            ras: RasDiagnosis(RasServiceState.Ok, running: true, "MitLicenseRas"),
            sql: SqlOk(running: true),
            iis: IisObjectState.Started);

        var snapshot = await provider.GetStatusAsync(CancellationToken.None);

        snapshot.OneCServers.Should().ContainSingle();
        snapshot.Ras.State.Should().Be("Ok");
        snapshot.Ras.Running.Should().BeTrue();
        snapshot.Sql.Running.Should().BeTrue();
        snapshot.Iis.State.Should().Be("Started");
        snapshot.Overall.Should().Be(ServerHealth.Healthy);
    }

    [Fact]
    public async Task GetStatus_degrades_single_source_without_failing_snapshot()
    {
        // RAS-диагностика бросает → его Available:false/Error, остальные ок, снимок не падает.
        var ras = Substitute.For<IRasServiceManager>();
        ras.DiagnoseAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new RasServiceOperationException("sc недоступен"));

        var provider = NewProvider(
            oneC: [new OneCServerStatus("1C Server", Running: true, "8.3.23.1865")],
            rasManager: ras,
            sql: SqlOk(running: true),
            iis: IisObjectState.Started);

        var snapshot = await provider.GetStatusAsync(CancellationToken.None);

        snapshot.Ras.Available.Should().BeFalse();
        snapshot.Ras.Error.Should().NotBeNullOrEmpty();
        snapshot.Sql.Available.Should().BeTrue();
        snapshot.Iis.Available.Should().BeTrue();
        // Запущенный ragent есть, но адаптер RAS недоступен → Degraded (а не падение).
        snapshot.Overall.Should().Be(ServerHealth.Degraded);
    }

    [Fact]
    public async Task GetStatus_iis_failure_marks_iis_unavailable()
    {
        var iis = Substitute.For<IIisLifecycleService>();
        iis.GetServerStateAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("W3SVC недоступна"));

        var provider = NewProvider(
            oneC: [new OneCServerStatus("1C Server", Running: true, "8.3.23.1865")],
            ras: RasDiagnosis(RasServiceState.Ok, running: true, "MitLicenseRas"),
            sql: SqlOk(running: true),
            iisService: iis);

        var snapshot = await provider.GetStatusAsync(CancellationToken.None);

        snapshot.Iis.Available.Should().BeFalse();
        snapshot.Overall.Should().Be(ServerHealth.Degraded);
    }

    [Fact]
    public async Task Overall_Down_when_no_running_ragent()
    {
        var provider = NewProvider(
            oneC: [new OneCServerStatus("1C Server", Running: false, "8.3.23.1865")],
            ras: RasDiagnosis(RasServiceState.Ok, running: true, "MitLicenseRas"),
            sql: SqlOk(running: true),
            iis: IisObjectState.Started);

        var snapshot = await provider.GetStatusAsync(CancellationToken.None);

        snapshot.Overall.Should().Be(ServerHealth.Down);
    }

    [Fact]
    public async Task Overall_Degraded_when_component_unhealthy()
    {
        // Запущенный ragent есть, SQL остановлена → Degraded.
        var provider = NewProvider(
            oneC: [new OneCServerStatus("1C Server", Running: true, "8.3.23.1865")],
            ras: RasDiagnosis(RasServiceState.Ok, running: true, "MitLicenseRas"),
            sql: SqlOk(running: false),
            iis: IisObjectState.Started);

        var snapshot = await provider.GetStatusAsync(CancellationToken.None);

        snapshot.Overall.Should().Be(ServerHealth.Degraded);
    }

    [Fact]
    public async Task Overall_Unknown_when_nothing_could_be_polled()
    {
        // Нет служб ragent + все адаптеры недоступны → опросить узел вообще не удалось.
        var ras = Substitute.For<IRasServiceManager>();
        ras.DiagnoseAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new RasServiceOperationException("sc"));
        var iis = Substitute.For<IIisLifecycleService>();
        iis.GetServerStateAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("w3svc"));

        var provider = NewProvider(
            oneC: [],
            rasManager: ras,
            sql: new SqlStatusSummary(null, null, false, Available: false, "sql down"),
            iisService: iis);

        var snapshot = await provider.GetStatusAsync(CancellationToken.None);

        snapshot.Overall.Should().Be(ServerHealth.Unknown);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────

    private static RasServiceDiagnosis RasDiagnosis(RasServiceState state, bool running, string serviceName) =>
        new(
            State: state,
            Service: new DiscoveredRasService(serviceName, running, "C:\\ras.exe", "8.5.1.1302", "1545"),
            Target: null,
            CommandPreview: null,
            TargetReady: true,
            Issue: null);

    private static SqlStatusSummary SqlOk(bool running) =>
        new("localhost", "MSSQLSERVER", running, Available: true, Error: null);

    private static ServerStatusProvider NewProvider(
        IReadOnlyList<OneCServerStatus> oneC,
        RasServiceDiagnosis? ras = null,
        IRasServiceManager? rasManager = null,
        SqlStatusSummary? sql = null,
        IisObjectState? iis = null,
        IIisLifecycleService? iisService = null)
    {
        // IOneCServerDiscovery/ISqlServiceStatusReader — internal в Infrastructure; NSubstitute
        // (DynamicProxyGenAssembly2) их проксировать не может, поэтому ручные фейки. RAS/IIS —
        // public интерфейсы Application, их мокаем через NSubstitute.
        var discovery = new FakeDiscovery(oneC);

        var rasMgr = rasManager ?? Substitute.For<IRasServiceManager>();
        if (rasManager is null && ras is not null)
        {
            rasMgr.DiagnoseAsync(Arg.Any<CancellationToken>()).Returns(ras);
        }

        var sqlReader = new FakeSqlReader(sql ?? SqlOk(running: true));

        var iisSvc = iisService ?? Substitute.For<IIisLifecycleService>();
        if (iisService is null && iis is not null)
        {
            iisSvc.GetServerStateAsync(Arg.Any<CancellationToken>()).Returns(iis.Value);
        }

        return new ServerStatusProvider(
            discovery, rasMgr, sqlReader, iisSvc, NullLogger<ServerStatusProvider>.Instance);
    }

    private sealed class FakeDiscovery : IOneCServerDiscovery
    {
        private readonly IReadOnlyList<OneCServerStatus> _servers;
        public FakeDiscovery(IReadOnlyList<OneCServerStatus> servers) => _servers = servers;
        public IReadOnlyList<OneCServerStatus> Discover() => _servers;
    }

    private sealed class FakeSqlReader : ISqlServiceStatusReader
    {
        private readonly SqlStatusSummary _summary;
        public FakeSqlReader(SqlStatusSummary summary) => _summary = summary;
        public SqlStatusSummary Read() => _summary;
    }
}
