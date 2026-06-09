namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job (MLC-077, ADR-27): ночная TTL-очистка бэкапов — server-side удаление .bak
// старше Settings.Backup.TtlHours (xp_delete_file через ISqlBackupService) + reap строк
// DatabaseBackups старше cutoff. Запускается в 03:15 UTC (смещено от 03:00 audit /
// 03:30 license-usage); CRON фиксирован в Program.cs, не настраивается оператором.
public interface IBackupRetentionJob
{
    Task RunAsync(CancellationToken ct);
}
