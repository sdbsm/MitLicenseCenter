using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Infrastructure.Reporting;

// Сущность-учёт бэкапов (MLC-076, ADR-27): одна строка = один запрошенный бэкап базы; таблица
// одновременно является очередью оркестратора (MLC-077): Queued-строки — очередь FIFO,
// Running — выполняющиеся. Живёт в Infrastructure.Reporting рядом с PerfRecording — это
// запись операций, читаемая Web напрямую через AppDbContext (vertical slice ADR-20), а не
// доменный агрегат. Намеренно НЕ в Infrastructure.Backups: тот неймспейс — адаптерный
// (SqlBackupAdapter), Web его не касается (guard LayerBoundaryTests). InfobaseId — простой
// Guid БЕЗ FK (как у LicenseUsageSnapshot/PerfRecording): запись переживает удаление
// инфобазы. Конфиг — inline в AppDbContext.OnModelCreating (как PerfRecording).
public sealed class DatabaseBackup : IEntity
{
    public Guid Id { get; init; }
    public Guid InfobaseId { get; init; }
    // Снимок server/db на момент запроса — бэкап адресует именно их, даже если инфобазу
    // потом переименуют/удалят.
    public string DatabaseServer { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
    public BackupStatus Status { get; set; }
    // Логин оператора, запросившего бэкап (паттерн PerfRecording.StartedBy).
    public string RequestedBy { get; init; } = string.Empty;
    public DateTime RequestedAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? FilePath { get; set; }
    public long? FileSizeBytes { get; set; }
    // None для Queued/Running/Succeeded; конкретная причина — только у Failed.
    public BackupFailureReason FailureReason { get; set; }
    public string? ErrorMessage { get; set; }
}
