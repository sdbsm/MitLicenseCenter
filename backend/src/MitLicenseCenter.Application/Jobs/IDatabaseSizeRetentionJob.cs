using Hangfire;

namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job (MLC-185c, близнец ILicenseUsageRetentionJob): ежедневная очистка
// dbo.DatabaseSizeSnapshots старше Settings.DatabaseSize.RetentionDays. Запускается в
// 04:00 UTC (свободный слот после занятых 03:00/03:15/03:30/03:45); CRON фиксирован в
// Program.cs, не настраивается оператором. Без аудит-записи — это housekeeping телеметрии
// (хватит server-лога).
public interface IDatabaseSizeRetentionJob
{
    // Идемпотентная ночная housekeeping-джоба: как и license-usage retention — 3 попытки на
    // транзиентные блипы, затем Fail; следующий суточный запуск самоисцеляется. Атрибут на
    // методе интерфейса (RecurringJob.AddOrUpdate<IDatabaseSizeRetentionJob>).
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    Task RunAsync(CancellationToken ct);
}
