using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Performance;

// Фоновый драйвер сэмплинга записи (MLC-070, ADR-26). Тонкая обёртка вокруг IPerfRecordingService
// (паттерн HotTierPollingService): на старте процесса один раз закрывает осиротевшие активные записи
// как Interrupted, затем тикает с периодом Performance.RecordingSampleIntervalSeconds и на каждом тике
// зовёт SampleOnceAsync — тот сам no-op'ит, если активной записи нет (как hot-tier при пустом списке).
// Вся логика (старт/стоп/авто-стоп, доступ к БД, время через TimeProvider) живёт в сервисе —
// детерминированном seam'е; здесь только тайминг, поэтому драйвер юнит-тестами не покрывается.
internal sealed partial class PerfRecordingSamplingService : BackgroundService
{
    private const int DefaultSampleIntervalSeconds = 15;

    private readonly IPerfRecordingService _recording;
    private readonly ISettingsSnapshot _settings;
    private readonly ILogger<PerfRecordingSamplingService> _logger;

    public PerfRecordingSamplingService(
        IPerfRecordingService recording,
        ISettingsSnapshot settings,
        ILogger<PerfRecordingSamplingService> logger)
    {
        _recording = recording;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger);

        // Best-effort: активная запись в БД после рестарта процесса (in-memory стейт потерян) → Interrupted.
        try
        {
            await _recording.RecoverInterruptedAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = _settings.GetInt(SettingKey.PerformanceRecordingSampleIntervalSeconds)
                ?? DefaultSampleIntervalSeconds;
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken).ConfigureAwait(false);

            if (!_recording.HasActiveRecording)
            {
                continue;
            }

            try
            {
                await _recording.SampleOnceAsync(stoppingToken).ConfigureAwait(false);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Perf recording sampling service started")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Perf recording sampling service stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Perf recording sampling tick failed")]
    private static partial void LogTickFailed(ILogger logger, Exception ex);
}
