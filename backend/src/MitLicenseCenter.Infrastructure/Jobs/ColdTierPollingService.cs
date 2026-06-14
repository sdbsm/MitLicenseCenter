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
internal sealed partial class ColdTierPollingService : BackgroundService, ISessionRefreshTrigger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISettingsSnapshot _settings;
    private readonly ILogger<ColdTierPollingService> _logger;

    // MLC-156: live-форс cold-обхода по запросу («Обновить сейчас» на /sessions).
    // _wakeSignal будит петлю ExecuteAsync досрочно (прерывает ожидание таймера), чтобы
    // прогон шёл немедленно. _pendingRun под _gate коалесцирует несколько одновременных
    // запросов в один ближайший прогон: все ждущие получают результат ОДНОГО тика.
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private readonly object _gate = new();
    private TaskCompletionSource? _pendingRun;

    public ColdTierPollingService(
        IServiceScopeFactory scopeFactory,
        ISettingsSnapshot settings,
        ILogger<ColdTierPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    // MLC-156: запросить немедленный cold-прогон и дождаться его завершения. Single-flight:
    // несколько параллельных вызовов схлопываются в один _pendingRun (один ближайший тик
    // петли), все ждущие завершаются вместе. Возвращает Task, который завершается, когда
    // соответствующий прогон закончился (успех → завершён, ошибка → пробрасывает исключение).
    public Task RunNowAsync(CancellationToken ct)
    {
        TaskCompletionSource tcs;
        lock (_gate)
        {
            // Переиспользуем уже ожидающий запрос, если он есть (коалесцирование):
            // следующий тик обслужит всех разом.
            _pendingRun ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs = _pendingRun;
        }

        // Будим петлю. Если сигнал уже взведён (CurrentCount == 1) — Release бросил бы
        // SemaphoreFullException; просто не дублируем (один сигнал достаточен — тик и так
        // заберёт _pendingRun).
        try
        {
            _wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Сигнал уже взведён — ничего не делаем.
        }

        return tcs.Task.WaitAsync(ct);
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

            // MLC-156: ждём либо тик таймера (false по таймауту), либо сигнал «Обновить
            // сейчас» (true — пробуждение досрочно). В обоих случаях прогоняем cold ниже.
            try
            {
                await _wakeSignal.WaitAsync(TimeSpan.FromSeconds(intervalSeconds), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            // Забираем ожидающий live-запрос ДО прогона: этот тик обслужит именно его.
            var pending = TakePendingRun();

            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
                pending?.TrySetResult();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                pending?.TrySetCanceled(stoppingToken);
                break;
            }
            catch (Exception ex)
            {
                // Не падаем: следующий тик самоисцелится (как прежний рекуррентный джоб).
                // Ждущий live-запрос получает исключение — фронт покажет ошибку обновления.
                LogColdFailed(_logger, ex);
                pending?.TrySetException(ex);
            }
        }

        // При остановке завершаем ещё не обслуженный live-запрос отменой, чтобы вызывающий
        // RunNowAsync не завис навсегда.
        TakePendingRun()?.TrySetCanceled(stoppingToken);

        LogStopped(_logger);
    }

    // Атомарно забирает текущий _pendingRun и обнуляет поле — следующие RunNowAsync
    // создадут новый запрос для следующего тика.
    private TaskCompletionSource? TakePendingRun()
    {
        lock (_gate)
        {
            var pending = _pendingRun;
            _pendingRun = null;
            return pending;
        }
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
