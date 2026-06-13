using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Backups;

namespace MitLicenseCenter.Infrastructure.Backups;

// Фоновый насос очереди бэкапов (MLC-077, ADR-27). Тонкая обёртка вокруг
// IBackupOrchestrator (образец PerfRecordingSamplingService): на старте процесса один раз
// закрывает осиротевшие Running как Failed/Interrupted, затем цикл «wake-сигнал ИЛИ
// таймаут» → ReapStuckRunningAsync → PumpOnceAsync. Reaper каждый тик закрывает зависшие
// Running (старше потолка времени) и снимает их замок-на-базу (MLC-123). Wake приходит от
// постановки в очередь и от завершения бэкапа (следующий из очереди стартует сразу);
// таймаут — страховочный плановый тик. Вся логика
// (потолок/замок/FIFO, доступ к БД, время) живёт в оркестраторе — детерминированном
// seam'е; здесь только тайминг, поэтому драйвер юнит-тестами не покрывается.
internal sealed partial class BackupPumpService : BackgroundService
{
    private static readonly TimeSpan TickTimeout = TimeSpan.FromSeconds(5);

    private readonly IBackupOrchestrator _orchestrator;
    private readonly ILogger<BackupPumpService> _logger;

    public BackupPumpService(IBackupOrchestrator orchestrator, ILogger<BackupPumpService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger);

        // Best-effort: Running-строки в БД после рестарта процесса (in-memory набор
        // выполняющихся потерян) → Failed/Interrupted; Queued переподхватятся ниже.
        try
        {
            await _orchestrator.RecoverInterruptedAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _orchestrator.WaitForWakeAsync(TickTimeout, stoppingToken).ConfigureAwait(false);
                // TTL-reaper зависших Running — каждый тик (MLC-123): закрывает строки старше
                // потолка времени выполнения и снимает их in-memory замок-на-базу, после чего
                // PumpOnceAsync может стартовать новый бэкап для разблокированной базы.
                await _orchestrator.ReapStuckRunningAsync(stoppingToken).ConfigureAwait(false);
                await _orchestrator.PumpOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogTickFailed(_logger, ex);
            }
        }

        LogStopped(_logger);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup pump service started")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup pump service stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Backup pump tick failed")]
    private static partial void LogTickFailed(ILogger logger, Exception ex);
}
