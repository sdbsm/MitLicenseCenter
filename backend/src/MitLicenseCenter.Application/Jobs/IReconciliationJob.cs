using Hangfire;

namespace MitLicenseCenter.Application.Jobs;

public interface IReconciliationJob
{
    // Recurring-job 'cold-snapshot' тикает раз в минуту, но один цикл под таймаутами
    // rac.exe может длиться дольше минуты (ListActiveSessions + re-fetch внутри
    // EnforceAsync, каждый — до 30с). Без распределённого лока следующий тик прошёл бы
    // и запустил вторую EnforceAsync параллельно первой → каждая независимо считает
    // over-limit и убивает сеансы newest-first до MaxKillsPerCycle → суммарный over-kill.
    // [DisableConcurrentExecution] (Hangfire SQL-storage distributed lock) гарантирует,
    // что одновременно исполняется только один enforcement-цикл — binding-требование
    // 02_ARCHITECTURE_REQUIREMENTS.md. Атрибут — на методе интерфейса, т.к. job
    // зарегистрирован через интерфейс (RecurringJob.AddOrUpdate<IReconciliationJob>),
    // и Hangfire берёт фильтры с зарегистрированного метода.
    //
    // Таймаут 180с с запасом перекрывает worst-case длительность цикла: перекрывающий
    // тик не падает, а ждёт освобождения лока и затем сам отсекается in-memory
    // ColdThrottleState — двойной EnforceAsync невозможен ни параллельно, ни вплотную.
    //
    // [AutomaticRetry(Attempts = 0)] (MLC-123, REL-22): цикл тикает раз в минуту и
    // самоисцеляется на следующем тике, поэтому ретраи бессмысленны и опасны — retry
    // под удержанным [DisableConcurrentExecution]-локом лишь копил бы очередь и шум.
    // Упавший цикл просто ждёт следующий минутный тик.
    [DisableConcurrentExecution(timeoutInSeconds: 180)]
    [AutomaticRetry(Attempts = 0)]
    Task RunColdAsync(CancellationToken ct);
}
