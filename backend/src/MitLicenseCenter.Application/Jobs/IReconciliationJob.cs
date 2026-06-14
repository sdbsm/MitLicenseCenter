namespace MitLicenseCenter.Application.Jobs;

public interface IReconciliationJob
{
    // MLC-154: «холодный» цикл согласования больше НЕ Hangfire-recurring — он крутится в
    // ColdTierPollingService (BackgroundService), чей таймер соблюдает Polling.ColdIntervalSeconds.
    // Поэтому прежние Hangfire-фильтры на этом методе сняты:
    //   - [DisableConcurrentExecution] (распределённый SQL-лок) — избыточен: single-host
    //     (ADR-28) + петля BackgroundService последовательна, а cold↔hot сериализует общий
    //     IEnforcementGate внутри RunColdAsync (защита от over-kill, MLC-001).
    //   - [AutomaticRetry(0)] — не нужен: упавший цикл самоисцеляется на следующем тике таймера.
    Task RunColdAsync(CancellationToken ct);
}
