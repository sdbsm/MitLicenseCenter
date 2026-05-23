namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job (PR 4.3): ежедневная очистка dbo.AuditLogs старше
// Settings.Audit.RetentionDays. Запускается в 03:00 UTC; CRON фиксирован
// в Program.cs, не настраивается оператором.
public interface IAuditRetentionJob
{
    Task RunAsync(CancellationToken ct);
}
