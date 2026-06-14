using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Jobs;

// MLC-154 (ADR-6.1): «холодный» цикл согласования сессий — общий снимок для дашборда и
// /sessions плюс promote/demote hot-тира и enforcement под общим замком. Раньше джоб был
// рекуррентным в Hangfire с CRON "* * * * *", но минимальная гранулярность 5-польного
// Hangfire-CRON = 1 минута, поэтому настройка Polling.ColdIntervalSeconds (10–300с) была
// инертна: полный обход шёл раз в минуту независимо от значения. Перенос на BackgroundService
// делает каданс реальным — Task.Delay читает интервал из ISettingsSnapshot КАЖДЫЙ цикл.
//
// Single-host (ADR-28): только один экземпляр службы, поэтому распределённый Hangfire-лок
// [DisableConcurrentExecution] здесь избыточен — петля BackgroundService последовательна
// сама по себе, а сериализацию cold↔hot обеспечивает общий IEnforcementGate внутри
// ReconciliationJob.RunColdAsync. Зеркалит HotTierPollingService.
internal sealed partial class ColdTierPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISettingsSnapshot _settings;
    private readonly ILogger<ColdTierPollingService> _logger;

    public ColdTierPollingService(
        IServiceScopeFactory scopeFactory,
        ISettingsSnapshot settings,
        ILogger<ColdTierPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger);

        // Немедленный warm-up: наполняем снимок сразу после старта, без окна «обновлено
        // ~2000 лет назад» (CapturedAtUtc = MinValue), пока не подошёл первый тик таймера.
        // Заменяет прежний стартовый BackgroundJob.Enqueue<IReconciliationJob>.
        try
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogStopped(_logger);
            return;
        }
        catch (Exception ex)
        {
            LogColdFailed(_logger, ex);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = _settings.GetInt(SettingKey.PollingColdIntervalSeconds) ?? 15;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken).ConfigureAwait(false);
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Не падаем: следующий тик самоисцелится (как прежний рекуррентный джоб).
                LogColdFailed(_logger, ex);
            }
        }

        LogStopped(_logger);
    }

    // Один прогон cold-цикла. Internal — детерминированный seam для теста (как
    // HotTierPollingService.RunCycleOnceAsync): без драйва таймера BackgroundService.
    // Оставлен internal для переиспользования будущим «форс-прогоном по запросу» (MLC-156).
    // ReconciliationJob — scoped (нужен DbContext), поэтому резолвим в собственном scope.
    internal async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<IReconciliationJob>();
        await job.RunColdAsync(ct).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cold-tier polling service started")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cold-tier polling service stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cold-tier polling cycle failed")]
    private static partial void LogColdFailed(ILogger logger, Exception ex);
}
