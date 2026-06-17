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
// панели оборвал выполнявшийся бэкап (файл может быть неполным); TimedOut — бэкап завис в
// Running дольше потолка времени выполнения, TTL-reaper насоса принудительно закрыл строку
// и снял in-memory замок-на-базу (файл на диске может быть неполным; MLC-123). int-значения
// ЗАМОРОЖЕНЫ — новые причины добавляются только в конец, существующие не переназначаются.
public enum BackupFailureReason
{
    None = 0,
    InsufficientSpace = 1,
    EstimateUnavailable = 2,
    PermissionDenied = 3,
    BackupFailed = 4,
    Interrupted = 5,
    TimedOut = 6,
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

// Предпоказ disk-guard ДО запуска бэкапа (MLC-183): оценка размера базы + свободное место
// на диске папки бэкапов + запас, чтобы оператор видел нехватку заранее. Единицы — БАЙТЫ
// (внутренние КБ/МБ адаптер конвертирует). EstimatedSizeBytes/FreeSpaceBytes = null при
// degraded (нет sysadmin / SQL недоступен / FILEPROPERTY вернул NULL / диск не найден).
// Sufficient = обе цифры не null И Estimated + SafetyMargin <= Free. Reason переиспользует
// замороженный BackupFailureReason (None — оценка получена; PermissionDenied/EstimateUnavailable/
// BackupFailed — degraded-причина). Это предпоказ, не стоп-кран: серверный disk-guard в
// BackupAsync остаётся единственным жёстким барьером.
public sealed record SqlBackupEstimate(
    long? EstimatedSizeBytes,
    long? FreeSpaceBytes,
    long SafetyMarginBytes,
    bool Sufficient,
    BackupFailureReason Reason);

// Итог постановки бэкапа в очередь (MLC-077): Queued — заведена новая строка;
// AlreadyActive — у этой пары (server, db) уже есть Queued/Running строка, эндпоинт
// отвечает 409 BACKUP_ACTIVE (паттерн PerfRecordingStartOutcome).
public enum BackupRequestOutcome
{
    Queued = 0,
    AlreadyActive = 1,
}

// BackupId — id новой Queued-строки либо СУЩЕСТВУЮЩЕЙ активной (для AlreadyActive фронт
// может сразу показать «уже идёт» по конкретной записи).
public sealed record BackupRequestResult(BackupRequestOutcome Outcome, Guid BackupId);
