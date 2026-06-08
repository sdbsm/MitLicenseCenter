using FluentAssertions;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Infrastructure.Performance.Testing;
using MitLicenseCenter.Web.Endpoints;
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
}
