using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Infrastructure.Reporting;

// Один сэмпл записи (MLC-070, ADR-26): снимок «сейчас» в момент SampleUtc. Метрики хоста уровня 1
// (сатурация) лежат плоскими колонками — по ним строится график host-во-времени при просмотре
// записи. Атрибуция по семьям (уровень 2) и точечные топ-виновники 1С/SQL переменного состава
// сериализованы в JSON-колонки (ProcessGroupsJson / OneCLoadJson / SqlLoadJson) — десериализуются
// эндпоинтом просмотра через PerfSampleJson. FK на PerfRecording с каскадным удалением.
public sealed class PerfRecordingSample : IEntity
{
    public Guid Id { get; init; }
    public Guid RecordingId { get; init; }
    public DateTime SampleUtc { get; init; }

    // Measuring=true, если в этот момент дельта-метрики (CPU%/латентность диска) ещё не готовы
    // (первый сэмпл после старта пробы) — просмотр рисует «измеряю…», а не нули как реальные значения.
    public bool Measuring { get; init; }

    // Уровень 1 «что насыщено» (host): CPU, RAM, диск. Плоские колонки — запросопригодны и дёшевы
    // для графика во времени.
    public double CpuPercent { get; init; }
    public double CpuQueueLength { get; init; }
    public double MemoryAvailableMBytes { get; init; }
    public double MemoryTotalMBytes { get; init; }
    public double MemoryPagesPerSec { get; init; }
    public double DiskAvgReadSecPerOp { get; init; }
    public double DiskAvgWriteSecPerOp { get; init; }
    public double DiskQueueLength { get; init; }

    // Сколько процессов проба не смогла прочитать из-за прав (атрибуция неполна) — честный сигнал,
    // как в live-снимке (MLC-064a).
    public int ProcessesInaccessible { get; init; }

    // Уровень 2 «кто потребляет»: атрибуция по семьям (JSON-массив ProcessGroupUsage).
    public string ProcessGroupsJson { get; init; } = "[]";

    // Точечные топ-виновники в момент сэмпла. null = источник не настроен/недоступен (best-effort,
    // как пустой live-снимок). OneCLoadJson — урезанный OneCLoadSnapshot (топ-сеансы + процессы);
    // SqlLoadJson — урезанный SqlPerformanceSnapshot (топ активных запросов + IO/wait).
    public string? OneCLoadJson { get; init; }
    public string? SqlLoadJson { get; init; }
}
