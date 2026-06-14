using MitLicenseCenter.Application.Backups;

namespace MitLicenseCenter.Web.Endpoints;

// Контракты бэкапов баз SQL (MLC-077, ADR-27). Status/FailureReason на проводе — строкой
// (JsonStringEnumConverter, Program.cs); nullable-поля null-опускаются глобальной policy
// (фронт читает их через omittable — урок MLC-067).

// Одна строка учёта/очереди бэкапов: и элемент списка, и detail-просмотр (полей немного,
// отдельный detail-DTO не нужен). FailureReason=None у Queued/Running/Succeeded.
public sealed record BackupSummary(
    Guid Id,
    Guid InfobaseId,
    string DatabaseServer,
    string DatabaseName,
    BackupStatus Status,
    string RequestedBy,
    DateTime RequestedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? FilePath,
    long? FileSizeBytes,
    BackupFailureReason FailureReason,
    string? ErrorMessage);

// Пагинированный список бэкапов (MLC-130, BE-17): конверт {items, total, page, pageSize}.
public sealed record BackupsPagedResponse(
    IReadOnlyList<BackupSummary> Items,
    int Total,
    int Page,
    int PageSize);

// POST /backups: инфобаза задаёт пару server+db (снимок берёт оркестратор).
public sealed record StartBackupRequest(Guid InfobaseId);
