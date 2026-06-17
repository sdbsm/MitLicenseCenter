using Hangfire;

namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job (MLC-185c): суточный сбор размеров баз. Один замер IDatabaseSizeProbe по
// всем базам инстанса, фильтр по базам инфобаз, запись DatabaseSizeSnapshot с единым
// SnapshotAtUtc. Запускается в 02:00 UTC (до ночных ретеншенов); CRON фиксирован в
// Program.cs, не настраивается оператором. Без аудит-записи — это телеметрия (хватит
// server-лога; запись в аудит жгла бы новый замороженный enum-номер).
public interface IDatabaseSizeCollectionJob
{
    // Идемпотентная ночная джоба: 3 попытки на транзиентные блипы, затем Fail; следующий
    // суточный запуск самоисцеляется. Атрибут на методе интерфейса
    // (RecurringJob.AddOrUpdate<IDatabaseSizeCollectionJob>).
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    Task RunAsync(CancellationToken ct);
}
