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
                ProcessGroups: [new ProcessGroupUsage("OneC", 0, 1000, 2)]),
        };

        var result = await PerformanceEndpoints.GetHostAsync(probe, CancellationToken.None);

        var body = result.Value!;
        body.Measuring.Should().BeTrue();
        body.ProcessGroups.Should().ContainSingle().Which.Family.Should().Be("OneC");
    }
}
