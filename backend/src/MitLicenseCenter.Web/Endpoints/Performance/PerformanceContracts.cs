using MitLicenseCenter.Application.Performance;

namespace MitLicenseCenter.Web.Endpoints;

// Контракты записи раздела «Быстродействие» (MLC-070, ADR-26, Фаза 4). Status/StopReason на проводе —
// строкой (JsonStringEnumConverter, Program.cs). Вложенные топ-виновники переиспользуют те же
// Application-записи, что и live-снимки (ProcessGroupUsage / OneCLoadSnapshot / SqlPerformanceSnapshot),
// чтобы фронт разбирал один формат и для live, и для записи.

// Элемент списка расследований + метаданные одной записи. SampleCount — число собранных сэмплов.
public sealed record RecordingSummary(
    Guid Id,
    DateTime StartedAtUtc,
    DateTime? StoppedAtUtc,
    PerfRecordingStatus Status,
    string StartedBy,
    PerfRecordingStopReason? StopReason,
    int SampleCount);

// Просмотр записи = метаданные + полный ряд сэмплов по времени (график host + топ-виновники за период).
public sealed record RecordingDetail(
    RecordingSummary Recording,
    IReadOnlyList<RecordingSampleDto> Samples);

// Один сэмпл при просмотре: host-метрики (плоские) + атрибуция по семьям + точечные виновники 1С/SQL.
// OneC/Sql = null, если в этот момент источник был не настроен/недоступен (best-effort, как live).
public sealed record RecordingSampleDto(
    DateTime SampleUtc,
    bool Measuring,
    double CpuPercent,
    double CpuQueueLength,
    double MemoryAvailableMBytes,
    double MemoryTotalMBytes,
    double MemoryPagesPerSec,
    double DiskAvgReadSecPerOp,
    double DiskAvgWriteSecPerOp,
    double DiskQueueLength,
    int ProcessesInaccessible,
    IReadOnlyList<ProcessGroupUsage> ProcessGroups,
    OneCLoadSnapshot? OneC,
    SqlPerformanceSnapshot? Sql);
