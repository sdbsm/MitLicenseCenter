using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Драйвер безопасного сбора ТЖ (MLC-230/231, 60_SAFETY №3/№4/№5). Тонкая обёртка-тайминг вокруг
// ITechLogCollectionService (зеркаль PerfRecordingSamplingService): вся логика — в сервисе
// (детерминированный seam, покрыт юнит-тестами через TimeProvider/фейк размера), здесь только порядок
// старта и период тика.
//
// ПОРЯДОК НА СТАРТЕ КРИТИЧЕН (60_SAFETY №5):
//   1) RecoverInterruptedAsync — осиротевшие после рестарта дела Active→Interrupted;
//   2) ReconcileOnStartupAsync — стартовая сверка файла: если в conf лежит НАШ logcfg, но активного
//      дела (после шага 1) нет — снять «забытый» конфиг.
// Так после рестарта осиротевший сбор и помечается прерванным, и его logcfg снимается → «сбор всегда
// снимается».
//
// Затем — петля с фиксированным коротким интервалом: на каждом тике MonitorActiveAsync сверяет окно
// времени (авто-стоп TimeLimit) и размер каталога сбора (авто-стоп DiskLimit); при отсутствии
// активного дела тик — no-op (как hot-tier при пустом списке).
internal sealed partial class TechLogWatchdogService : BackgroundService
{
    // Фиксированный короткий интервал сторожа (без отдельной настройки): лимит места критичен —
    // полный ТЖ забивает диск за минуты (MLC-229), поэтому сверяем чаще запаса.
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(20);

    private readonly ITechLogCollectionService _collections;
    private readonly ILogger<TechLogWatchdogService> _logger;

    public TechLogWatchdogService(
        ITechLogCollectionService collections,
        ILogger<TechLogWatchdogService> logger)
    {
        _collections = collections;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger);

        // Старт: orphan-recovery → стартовая сверка файла (порядок критичен, см. заголовок).
        try
        {
            await _collections.RecoverInterruptedAsync(stoppingToken).ConfigureAwait(false);
            await _collections.ReconcileOnStartupAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Остановка хоста во время старта — штатно.
            return;
        }

        // Петля сторожа активного дела: окно времени + лимит места.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TickInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (!_collections.HasActiveCollection)
            {
                continue;
            }

            try
            {
                await _collections.MonitorActiveAsync(stoppingToken).ConfigureAwait(false);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Tech-log watchdog service started")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tech-log watchdog service stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tech-log watchdog tick failed")]
    private static partial void LogTickFailed(ILogger logger, Exception ex);
}
