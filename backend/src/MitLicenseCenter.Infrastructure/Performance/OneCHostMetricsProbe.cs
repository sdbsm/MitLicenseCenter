using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Performance;

// Реальный адаптер host-метрик (MLC-064, ADR-26). Источники:
//   • CPU / RAM / очередь — WMI Win32_PerfFormattedData_* (готовые значения);
//   • латентность диска — WMI Win32_PerfRawData_PerfDisk_PhysicalDisk + ручной cook
//     PERF_AVERAGE_TIMER по дельте двух замеров (формат-данные дают целые секунды → 0);
//   • всего RAM — Win32_ComputerSystem (кэшируется, не меняется);
//   • CPU%/RAM процессов — System.Diagnostics.Process (TotalProcessorTime + WorkingSet64).
//
// Почему WMI, а не PerformanceCounter: имена perf-категорий/счётчиков ЛОКАЛИЗОВАНЫ на RU
// Windows («Процессор» / «% загруженности процессора»), и English-lookup PerformanceCounter
// падает с «category does not exist». WQL-свойства perf-классов инвариантны (см. CLAUDE.md
// «Гочи Windows/1С»). Стенд трека — RU Windows (CP866), поэтому WMI — надёжный путь.
//
// Дельта CPU% процессов и латентность диска требуют ДВУХ замеров → singleton держит
// предыдущий снимок (паттерн ColdThrottleState/IClusterUuidCache). Первый poll →
// Measuring=true (эти метрики ещё 0). Windows-only: WMI-perf-классы и Process-метрики
// доступны только на Windows; в DI регистрируется под #pragma CA1416, в тестах —
// StubHostMetricsProbe. Защитный: сбой отдельного WQL-запроса логируется и деградирует
// в 0, снимок всегда возвращается (best-effort live, ADR-26).
[SupportedOSPlatform("windows")]
internal sealed partial class OneCHostMetricsProbe : IHostMetricsProbe
{
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<OneCHostMetricsProbe> _logger;

    private readonly object _gate = new();
    private PrevSample? _previous;
    private double _totalRamMBytes; // кэш Win32_ComputerSystem (0 = ещё не прочитан/недоступен)

    public OneCHostMetricsProbe(
        ISettingsSnapshot settings, TimeProvider clock, ILogger<OneCHostMetricsProbe> logger)
    {
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public Task<HostMetricsSnapshot> CaptureAsync(CancellationToken ct)
    {
        var map = ProcessFamilyMap.Parse(_settings.GetString(SettingKey.PerformanceProcessFamilyMap));
        var now = _clock.GetUtcNow().UtcDateTime;

        var cpu = ReadCpu();
        var memory = ReadMemory();
        var diskRaw = ReadDiskRaw();
        var processes = ReadProcesses();

        lock (_gate)
        {
            var measuring = _previous is null;

            var samples = BuildSamples(processes, _previous, now);
            var groups = ProcessFamilyGrouping.Group(samples, map);
            var disk = CookDisk(diskRaw, _previous?.Disk);

            _previous = new PrevSample(
                now,
                processes.ToDictionary(p => p.Pid, p => new ProcCpu(p.Name, p.Cpu)),
                diskRaw.Stored);

            var snapshot = new HostMetricsSnapshot(now, measuring, cpu, memory, disk, groups);
            return Task.FromResult(snapshot);
        }
    }

    // ── Уровень 1: сатурация хоста (WMI) ───────────────────────────────────────────

    private CpuMetrics ReadCpu()
    {
        var percent = ReadScalar(
            "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'",
            "PercentProcessorTime");
        var queue = ReadScalar(
            "SELECT ProcessorQueueLength FROM Win32_PerfFormattedData_PerfOS_System",
            "ProcessorQueueLength");
        return new CpuMetrics(percent, queue);
    }

    private MemoryMetrics ReadMemory()
    {
        double available = 0, pages = 0;
        TryQueryFirst(
            "SELECT AvailableMBytes, PagesPerSec FROM Win32_PerfFormattedData_PerfOS_Memory",
            mo =>
            {
                available = ToDouble(mo["AvailableMBytes"]);
                pages = ToDouble(mo["PagesPerSec"]);
            });
        return new MemoryMetrics(available, ReadTotalRamMBytes(), pages);
    }

    private DiskSample ReadDiskRaw()
    {
        var sample = default(DiskSample);
        TryQueryFirst(
            "SELECT AvgDiskSecPerRead, AvgDiskSecPerRead_Base, AvgDiskSecPerWrite, " +
            "AvgDiskSecPerWrite_Base, CurrentDiskQueueLength, Frequency_PerfTime " +
            "FROM Win32_PerfRawData_PerfDisk_PhysicalDisk WHERE Name='_Total'",
            mo => sample = new DiskSample(
                new DiskRaw(
                    ToULong(mo["AvgDiskSecPerRead"]),
                    ToULong(mo["AvgDiskSecPerRead_Base"]),
                    ToULong(mo["AvgDiskSecPerWrite"]),
                    ToULong(mo["AvgDiskSecPerWrite_Base"]),
                    ToULong(mo["Frequency_PerfTime"])),
                QueueLength: ToDouble(mo["CurrentDiskQueueLength"])));
        return sample;
    }

    // Cook PERF_AVERAGE_TIMER: latency_sec = ((N1-N0)/Freq)/(Base1-Base0). Без предыдущего
    // замера (первый poll) или без операций в интервале → 0. Очередь — мгновенная.
    private static DiskMetrics CookDisk(DiskSample current, DiskRaw? previous)
    {
        if (previous is not { } prev || current.Stored.Frequency == 0)
        {
            return new DiskMetrics(0, 0, current.QueueLength);
        }

        var read = AverageTimer(current.Stored.Read, prev.Read, current.Stored.ReadBase, prev.ReadBase, current.Stored.Frequency);
        var write = AverageTimer(current.Stored.Write, prev.Write, current.Stored.WriteBase, prev.WriteBase, current.Stored.Frequency);
        return new DiskMetrics(read, write, current.QueueLength);
    }

    private static double AverageTimer(ulong n1, ulong n0, ulong b1, ulong b0, ulong freq)
    {
        // Счётчик/база сбросились (рестарт PerfLib) или нет операций → 0.
        if (n1 < n0 || b1 <= b0)
        {
            return 0;
        }
        return (double)(n1 - n0) / freq / (b1 - b0);
    }

    private double ReadTotalRamMBytes()
    {
        if (_totalRamMBytes > 0)
        {
            return _totalRamMBytes;
        }
        TryQueryFirst(
            "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem",
            mo => _totalRamMBytes = ToULong(mo["TotalPhysicalMemory"]) / (1024d * 1024d));
        return _totalRamMBytes;
    }

    // ── Уровень 2: процессы (System.Diagnostics.Process) ───────────────────────────

    private static List<ProcInfo> ReadProcesses()
    {
        var infos = new List<ProcInfo>();
        foreach (var proc in Process.GetProcesses())
        {
            using (proc)
            {
                try
                {
                    // TotalProcessorTime / WorkingSet64 могут бросить для защищённых или
                    // только что завершившихся процессов — такой просто пропускаем.
                    infos.Add(new ProcInfo(proc.Id, proc.ProcessName, proc.TotalProcessorTime, proc.WorkingSet64));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Idle/System и часть системных процессов недоступны — это норма, не шумим.
                }
            }
        }
        return infos;
    }

    // CPU% процесса = дельта CPU-времени / (прошедшее время × число ядер) × 100. Сопоставление
    // по PID+имя (защита от переиспользования PID). Без предыдущего снимка / нового процесса → 0.
    private static List<ProcessSample> BuildSamples(List<ProcInfo> processes, PrevSample? previous, DateTime now)
    {
        var elapsedSeconds = previous is { } p ? (now - p.CapturedUtc).TotalSeconds : 0;
        var cores = Environment.ProcessorCount;
        var samples = new List<ProcessSample>(processes.Count);

        foreach (var info in processes)
        {
            double cpuPercent = 0;
            if (previous is { } prev
                && elapsedSeconds > 0
                && prev.Processes.TryGetValue(info.Pid, out var before)
                && string.Equals(before.Name, info.Name, StringComparison.Ordinal))
            {
                var deltaSeconds = (info.Cpu - before.Cpu).TotalSeconds;
                cpuPercent = Math.Max(0, deltaSeconds / (elapsedSeconds * cores) * 100);
            }

            samples.Add(new ProcessSample(info.Name, cpuPercent, info.Ram));
        }

        return samples;
    }

    // ── WMI-обвязка ────────────────────────────────────────────────────────────────

    private double ReadScalar(string wql, string property)
    {
        double value = 0;
        TryQueryFirst(wql, mo => value = ToDouble(mo[property]));
        return value;
    }

    private void TryQueryFirst(string wql, Action<ManagementObject> read)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            using var results = searcher.Get();
            foreach (ManagementBaseObject obj in results)
            {
                using (obj)
                {
                    if (obj is ManagementObject mo)
                    {
                        read(mo);
                    }
                    return; // только первая строка (_Total / единственный объект)
                }
            }
        }
        catch (ManagementException ex)
        {
            LogWmiQueryFailed(_logger, wql, ex);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            LogWmiQueryFailed(_logger, wql, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogWmiQueryFailed(_logger, wql, ex);
        }
    }

    private static double ToDouble(object? value) =>
        value is null ? 0 : Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static ulong ToULong(object? value) =>
        value is null ? 0 : Convert.ToUInt64(value, CultureInfo.InvariantCulture);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Host-метрики: WMI-запрос не выполнен ({Wql})")]
    private static partial void LogWmiQueryFailed(ILogger logger, string wql, Exception ex);

    private sealed record PrevSample(
        DateTime CapturedUtc,
        IReadOnlyDictionary<int, ProcCpu> Processes,
        DiskRaw Disk);

    private readonly record struct ProcCpu(string Name, TimeSpan Cpu);

    private readonly record struct ProcInfo(int Pid, string Name, TimeSpan Cpu, long Ram);

    // Сырые perf-счётчики диска (perf-time ticks) — для дельта-cook между poll'ами.
    private readonly record struct DiskRaw(ulong Read, ulong ReadBase, ulong Write, ulong WriteBase, ulong Frequency);

    private readonly record struct DiskSample(DiskRaw Stored, double QueueLength);
}
