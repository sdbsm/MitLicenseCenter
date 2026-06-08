namespace MitLicenseCenter.Application.Performance;

// Live-снимок метрик хоста для раздела «Быстродействие» (MLC-064, ADR-26). Адаптер
// читает источники по требованию (pull) и НИЧЕГО не персистит — live-модель ADR-26.
// Реализация — OneCHostMetricsProbe (Infrastructure, WMI + System.Diagnostics.Process,
// Windows-only за ADR-20); в тестах — StubHostMetricsProbe.
public interface IHostMetricsProbe
{
    // Снимает текущие метрики хоста. Никогда не бросает ради инфраструктурного сбоя
    // отдельного счётчика — недоступная метрика деградирует в 0 (best-effort live).
    Task<HostMetricsSnapshot> CaptureAsync(CancellationToken ct);
}

// Нейтральный снимок «сейчас». Measuring=true на первом poll'е: метрики, требующие
// дельты между двумя замерами (CPU% процессов, латентность диска), ещё не готовы —
// фронт показывает «измеряю…». Уровень 1 (сатурация) — Cpu/Memory/Disk; уровень 2
// (кто потребляет) — ProcessGroups по семьям (ADR-26, методика «светофор + атрибуция»).
//
// ProcessesInaccessible — сколько процессов адаптер НЕ смог прочитать из-за нехватки
// прав (их CPU/RAM не попали в атрибуцию). Под недостаточно привилегированным backend'ом
// сюда уходят rphost/sqlservr чужих служебных учёток → раздел рискует показать ложное
// «всё Прочее». Производный AttributionIncomplete=true сигналит фронту нарисовать баннер
// (паттерн честных сигналов проекта: IIS-permissions, readiness, Measuring; MLC-064a).
public sealed record HostMetricsSnapshot(
    DateTime CapturedAtUtc,
    bool Measuring,
    CpuMetrics Cpu,
    MemoryMetrics Memory,
    DiskMetrics Disk,
    IReadOnlyList<ProcessGroupUsage> ProcessGroups,
    int ProcessesInaccessible)
{
    // Атрибуция неполна, если хотя бы один процесс остался непрочитанным из-за прав:
    // его потребление выпало из семей, и доли по семьям занижены/искажены.
    public bool AttributionIncomplete => ProcessesInaccessible > 0;
}

// CPU: общий % загрузки + длина очереди процессора (сатурация — очередь важнее голого %).
public sealed record CpuMetrics(double TotalPercent, double QueueLength);

// RAM: доступно МБ + всего МБ + Pages/sec (страничный обмен = ранний признак нехватки).
public sealed record MemoryMetrics(double AvailableMBytes, double TotalMBytes, double PagesPerSec);

// Диск (_Total): средняя латентность чтения/записи (секунды на операцию) + длина очереди.
// Латентность — главный сигнал насыщения диска; на первом poll'е = 0 (Measuring).
public sealed record DiskMetrics(double AvgReadSecPerOp, double AvgWriteSecPerOp, double QueueLength);

// Потребление ресурса семьёй процессов (1С / MSSQL / обновления ОС / антивирус / прочее).
// CpuPercent суммарный по семье; на первом poll'е 0 для всех (Measuring). RamBytes — сумма
// рабочих наборов. ProcessCount — сколько процессов в семье сейчас живо.
public sealed record ProcessGroupUsage(string Family, double CpuPercent, long RamBytes, int ProcessCount);
