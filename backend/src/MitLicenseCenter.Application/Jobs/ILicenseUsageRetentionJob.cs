using Hangfire;

namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job (MLC-048, ADR-25): ежедневная очистка dbo.LicenseUsageSnapshots старше
// Settings.LicenseUsage.RetentionDays. Запускается в 03:30 UTC (смещён от audit-retention
// 03:00); CRON фиксирован в Program.cs, не настраивается оператором. Без аудит-записи —
// это housekeeping телеметрии (хватит server-лога).
public interface ILicenseUsageRetentionJob
{
    // Идемпотентная ночная housekeeping-джоба (MLC-123, REL-22): как и audit-retention —
    // 3 попытки на транзиентные блипы, затем Fail; следующий суточный запуск самоисцеляется.
    // Атрибут на методе интерфейса (RecurringJob.AddOrUpdate<ILicenseUsageRetentionJob>).
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    Task RunAsync(CancellationToken ct);
}
