namespace MitLicenseCenter.Application.Backups;

// Жизненный цикл бэкапа (MLC-076, ADR-27). На проводе — строкой (JsonStringEnumConverter,
// Program.cs); в БД хранится int'ом (HasConversion<int>). Это первичное определение —
// int-значения не переиспользовать (та же дисциплина, что у замороженных enum'ов аудита
// и PerfRecordingStatus).
public enum BackupStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
}

// Типизированная причина провала бэкапа — честный degraded-сигнал адаптера (паттерн
// SqlProbeStatus MLC-068): None — успех/ещё не завершён; InsufficientSpace — на диске
// нет места под оценку + запас; EstimateUnavailable — оценку размера получить не удалось
// (без оценки не стартуем, ADR-27); PermissionDenied — учётка панели не sysadmin;
// BackupFailed — сам BACKUP/VERIFY или инфраструктура упали; Interrupted — рестарт
// панели оборвал выполнявшийся бэкап (файл может быть неполным). int-значения ЗАМОРОЖЕНЫ.
public enum BackupFailureReason
{
    None = 0,
    InsufficientSpace = 1,
    EstimateUnavailable = 2,
    PermissionDenied = 3,
    BackupFailed = 4,
    Interrupted = 5,
}

// Итог одной операции бэкапа. Провал — Succeeded=false + Reason + человекочитаемый
// ErrorMessage (с числами для InsufficientSpace); успех — путь и размер готового .bak
// (размер из msdb может отсутствовать — null, не провал).
public sealed record SqlBackupResult(
    bool Succeeded,
    BackupFailureReason Reason,
    string? FilePath,
    long? FileSizeBytes,
    string? ErrorMessage);

// Итог server-side удаления устаревших .bak (TTL-джоба / Admin-удаление).
public sealed record SqlDeleteResult(bool Succeeded, string? ErrorMessage);
