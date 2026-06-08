namespace MitLicenseCenter.Application.Performance;

// Сырой замер одного процесса — нейтральный вход чистой группировки (без
// System.Diagnostics.Process, чтобы логику можно было тестировать без WMI/ОС).
public sealed record ProcessSample(string ProcessName, double CpuPercent, long RamBytes);

// Чистая агрегация процессов в семьи (MLC-064). Адаптер собирает ProcessSample'ы из ОС
// и зовёт Group — вся доменная логика атрибуции здесь, под unit-тестом без WMI.
public static class ProcessFamilyGrouping
{
    // Группирует процессы в семьи по карте: CPU% и RAM суммируются, считается число
    // процессов. Сортировка по убыванию CPU (затем имя семьи) — сверху главный потребитель.
    public static IReadOnlyList<ProcessGroupUsage> Group(IEnumerable<ProcessSample> samples, ProcessFamilyMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        var acc = new Dictionary<string, Running>(StringComparer.Ordinal);
        foreach (var s in samples)
        {
            var family = map.Classify(s.ProcessName);
            if (acc.TryGetValue(family, out var r))
            {
                r.Cpu += s.CpuPercent;
                r.Ram += s.RamBytes;
                r.Count++;
            }
            else
            {
                acc[family] = new Running { Cpu = s.CpuPercent, Ram = s.RamBytes, Count = 1 };
            }
        }

        return acc
            .Select(kv => new ProcessGroupUsage(kv.Key, kv.Value.Cpu, kv.Value.Ram, kv.Value.Count))
            .OrderByDescending(g => g.CpuPercent)
            .ThenBy(g => g.Family, StringComparer.Ordinal)
            .ToList();
    }

    private sealed class Running
    {
        public double Cpu;
        public long Ram;
        public int Count;
    }
}
