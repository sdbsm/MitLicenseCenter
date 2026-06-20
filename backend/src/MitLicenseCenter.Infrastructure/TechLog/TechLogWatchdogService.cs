using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Сторож сбора ТЖ на старте процесса (MLC-230, 60_SAFETY №5). Тонкая обёртка-драйвер (зеркаль
// PerfRecordingSamplingService-регистрации): на старте один раз сверяет фактический logcfg.xml в
// conf с ожидаемым состоянием — если в conf лежит НАШ конфиг без активного дела (краш ОС оставил
// «забытый» конфиг), принудительно восстанавливает исходный. Вся логика — в
// ITechLogCollectionService.ReconcileOnStartupAsync (детерминированный seam, покрыт юнит-тестами);
// здесь только запуск на старте. Полный orphan-recovery (Active→Interrupted) и периодическая петля
// авто-стопа — MLC-231 (здесь только стартовая сверка файла).
internal sealed partial class TechLogWatchdogService : BackgroundService
{
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

        try
        {
            await _collections.ReconcileOnStartupAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Остановка хоста во время сверки — штатно.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Tech-log watchdog service started")]
    private static partial void LogStarted(ILogger logger);
}
