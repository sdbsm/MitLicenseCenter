using Hangfire;

namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job (PR 4.3): ежедневная очистка dbo.AuditLogs старше
// Settings.Audit.RetentionDays. Запускается в 03:00 UTC; CRON фиксирован
// в Program.cs, не настраивается оператором.
public interface IAuditRetentionJob
{
    // Идемпотентная ночная housekeeping-джоба (MLC-123, REL-22): дефолтные 10 ретраев
    // Hangfire — лишний красный шум при стойком сбое БД. Берём 3 попытки на транзиентные
    // блипы (deadlock/таймаут лога), затем Fail — упавшая джоба остаётся видимой, а
    // следующий суточный запуск самоисцеляется (cutoff пересчитывается от now). Атрибут —
    // на методе интерфейса: job зарегистрирован через интерфейс
    // (RecurringJob.AddOrUpdate<IAuditRetentionJob>), Hangfire берёт фильтры с него.
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    Task RunAsync(CancellationToken ct);
}
