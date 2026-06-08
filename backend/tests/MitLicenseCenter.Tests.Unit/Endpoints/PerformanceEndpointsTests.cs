using FluentAssertions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Infrastructure.Performance.Testing;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-064: эндпоинт GET /performance/host отдаёт live-снимок порта IHostMetricsProbe
// как есть (vertical slice ADR-20). Прогоняем через StubHostMetricsProbe — без WMI.
public sealed class PerformanceEndpointsTests
{
    [Fact]
    public async Task GetHost_returns_probe_snapshot()
    {
        var probe = new StubHostMetricsProbe();

        var result = await PerformanceEndpoints.GetHostAsync(probe, CancellationToken.None);

        result.Value.Should().BeSameAs(probe.Snapshot);
        probe.CaptureCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetHost_passes_through_measuring_flag_and_groups()
    {
        var probe = new StubHostMetricsProbe
        {
            Snapshot = new HostMetricsSnapshot(
                CapturedAtUtc: new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc),
                Measuring: true,
                Cpu: new CpuMetrics(0, 0),
                Memory: new MemoryMetrics(1024, 8192, 0),
                Disk: new DiskMetrics(0, 0, 0),
                ProcessGroups: [new ProcessGroupUsage("OneC", 0, 1000, 2)],
                ProcessesInaccessible: 0),
        };

        var result = await PerformanceEndpoints.GetHostAsync(probe, CancellationToken.None);

        var body = result.Value!;
        body.Measuring.Should().BeTrue();
        body.ProcessGroups.Should().ContainSingle().Which.Family.Should().Be("OneC");
    }

    // MLC-064a: число недоступных процессов проходит через эндпоинт, а производный признак
    // «атрибуция неполна» взводится при наличии хотя бы одного непрочитанного процесса.
    [Fact]
    public async Task GetHost_passes_through_inaccessible_count_and_derives_incomplete_flag()
    {
        var probe = new StubHostMetricsProbe
        {
            Snapshot = new HostMetricsSnapshot(
                CapturedAtUtc: new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc),
                Measuring: false,
                Cpu: new CpuMetrics(5, 0),
                Memory: new MemoryMetrics(1024, 8192, 0),
                Disk: new DiskMetrics(0, 0, 0),
                ProcessGroups: [new ProcessGroupUsage("Other", 5, 1000, 4)],
                ProcessesInaccessible: 7),
        };

        var result = await PerformanceEndpoints.GetHostAsync(probe, CancellationToken.None);

        var body = result.Value!;
        body.ProcessesInaccessible.Should().Be(7);
        body.AttributionIncomplete.Should().BeTrue();
    }

    [Fact]
    public void AttributionIncomplete_is_false_when_all_processes_readable()
    {
        var snapshot = new HostMetricsSnapshot(
            CapturedAtUtc: new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc),
            Measuring: false,
            Cpu: new CpuMetrics(5, 0),
            Memory: new MemoryMetrics(1024, 8192, 0),
            Disk: new DiskMetrics(0, 0, 0),
            ProcessGroups: [],
            ProcessesInaccessible: 0);

        snapshot.AttributionIncomplete.Should().BeFalse();
    }

    // MLC-066: GET /performance/onec-sessions компонует live-снимок из двух Application-портов
    // IClusterClient (сеансы с perf + рабочие процессы) — vertical slice ADR-20, без rac.exe в Web.
    [Fact]
    public async Task GetOneCSessions_composes_session_loads_and_processes()
    {
        var cluster = Substitute.For<IClusterClient>();
        var session = new OneCSessionLoad(
            SessionId: Guid.Parse("02d5184c-65b5-4d8a-ae39-b156b909fcaf"),
            SessionNumber: 1,
            ClusterInfobaseId: Guid.Parse("6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa"),
            AppId: "1CV8C", UserName: "Андрей", Host: "ANDREY-PC",
            Process: Guid.Parse("487281d5-aaaa-bbbb-cccc-ddddeeeeffff"), Connection: null,
            CpuTimeCurrent: 109, DurationCurrent: 422, DurationCurrentDbms: 0,
            MemoryCurrent: -1138560, BlockedByDbms: 0, BlockedByLs: 0,
            LastActiveAtUtc: new DateTime(2026, 6, 8, 20, 21, 45, DateTimeKind.Utc));
        var process = new OneCProcessLoad(
            Process: Guid.Parse("487281d5-aaaa-bbbb-cccc-ddddeeeeffff"),
            Pid: 15876, AvailablePerformance: 416, AvgCallTime: 1.124, MemorySize: 1682404);

        cluster.ListSessionLoadsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OneCSessionLoad>>([session]));
        cluster.ListProcessesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OneCProcessLoad>>([process]));

        var result = await PerformanceEndpoints.GetOneCSessionsAsync(cluster, CancellationToken.None);

        var body = result.Value!;
        body.Sessions.Should().ContainSingle().Which.MemoryCurrent.Should().Be(-1138560);
        body.Processes.Should().ContainSingle().Which.AvailablePerformance.Should().Be(416);
        body.CapturedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetOneCSessions_returns_empty_snapshot_when_cluster_unavailable()
    {
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListSessionLoadsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OneCSessionLoad>>([]));
        cluster.ListProcessesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<OneCProcessLoad>>([]));

        var result = await PerformanceEndpoints.GetOneCSessionsAsync(cluster, CancellationToken.None);

        result.Value!.Sessions.Should().BeEmpty();
        result.Value!.Processes.Should().BeEmpty();
    }
}
