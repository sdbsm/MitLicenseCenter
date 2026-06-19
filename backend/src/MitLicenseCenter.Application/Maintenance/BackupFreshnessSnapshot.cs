namespace MitLicenseCenter.Application.Maintenance;

// Снимок свежести бэкапов раздела «Обслуживание» (MLC-216). Деградация — статусом (как
// SqlPerformanceProbe): Ok — прочитали backupset; PermissionDenied — нет прав на
// msdb.dbo.backupset (честный сигнал, а не пустое «всё свежо»); Unavailable — SQL недоступен
// / строка подключения не настроена. В degraded-ветках Databases пуст.
public sealed record BackupFreshnessSnapshot(
    MaintenanceProbeStatus Status,
    IReadOnlyList<DatabaseBackupFreshness> Databases);

// Свежесть последнего бэкапа одной пользовательской базы. LastFull/LastDiff/LastLog — время
// завершения последнего бэкапа соответствующего типа (UTC), null — бэкапа такого типа в
// истории нет. IsStale — вычисленный флаг «устарел» (см. BackupFreshnessPolicy): нет ни одного
// FULL-бэкапа либо последний FULL старше порога свежести.
public sealed record DatabaseBackupFreshness(
    string DatabaseName,
    DateTime? LastFullUtc,
    DateTime? LastDiffUtc,
    DateTime? LastLogUtc,
    bool IsStale);

// Статус пробы обслуживания. Строкой на проводе (как SqlProbeStatus) — forward-compat границы FE.
public enum MaintenanceProbeStatus
{
    Ok,
    PermissionDenied,
    Unavailable,
}
