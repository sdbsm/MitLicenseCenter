using MitLicenseCenter.Application.Performance;

namespace MitLicenseCenter.Infrastructure.Performance.Testing;

// Заглушка для unit-тестов (endpoint /performance/host), которым не нужны реальные WMI /
// Process. В production-DI не регистрируется — реальный OneCHostMetricsProbe требует
// Windows. Настраивается публичным полем Snapshot.
internal sealed class StubHostMetricsProbe : IHostMetricsProbe
{
    // Снимок, который вернёт CaptureAsync. По умолчанию — правдоподобный «всё спокойно».
    public HostMetricsSnapshot Snapshot { get; set; } = new(
        CapturedAtUtc: new DateTime(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc),
        Measuring: false,
        Cpu: new CpuMetrics(TotalPercent: 12, QueueLength: 0),
        Memory: new MemoryMetrics(AvailableMBytes: 8192, TotalMBytes: 16384, PagesPerSec: 0),
        Disk: new DiskMetrics(AvgReadSecPerOp: 0.002, AvgWriteSecPerOp: 0.003, QueueLength: 0),
        ProcessGroups:
        [
            new ProcessGroupUsage("OneC", CpuPercent: 8, RamBytes: 1_500_000_000, ProcessCount: 3),
            new ProcessGroupUsage("Mssql", CpuPercent: 3, RamBytes: 2_000_000_000, ProcessCount: 1),
        ]);

    public int CaptureCalls { get; private set; }

    public Task<HostMetricsSnapshot> CaptureAsync(CancellationToken ct)
    {
        CaptureCalls++;
        return Task.FromResult(Snapshot);
    }
}
