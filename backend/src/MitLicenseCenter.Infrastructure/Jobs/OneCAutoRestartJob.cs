using System.Globalization;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Server;

namespace MitLicenseCenter.Infrastructure.Jobs;

// MLC-218 (ADR-55): ночной профилактический рестарт сервера 1С. Тело — найти ЗАПУЩЕННЫЕ
// службы ragent через discovery (MLC-213) и рестартнуть каждую через надёжный
// IWindowsServiceController (MLC-212, верификация состояния встроена), затем записать
// аудит срабатывания (initiator "system", код 803) и обновить OneC.AutoRestart.LastRunUtc.
//
// Защита от рассинхрона: в начале проверяем OneC.AutoRestart.Enabled через store (а не
// 30-сек snapshot) — если оператор только что выключил расписание, а задание ещё висит
// в Hangfire-сторадже, джоба = no-op (не рестартит). Cron регистрируется/снимается из
// эндпоинта /server/auto-restart (НЕ тик-каждые-5-минут).
//
// [AutomaticRetry(Attempts = 0)] объявлен на интерфейсе: рестарт разрушителен, повтор при
// сбое не нужен — самоисцеление на следующий день. Per-service-сбой не валит всю джобу:
// логируем и продолжаем (одна сломанная служба не должна мешать рестарту остальных).
internal sealed partial class OneCAutoRestartJob : IOneCAutoRestartJob
{
    private const string SystemInitiator = "system";

    private readonly IOneCServerDiscovery _discovery;
    private readonly IWindowsServiceController _controller;
    private readonly ISettingsStore _settings;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly ILogger<OneCAutoRestartJob> _logger;

    public OneCAutoRestartJob(
        IOneCServerDiscovery discovery,
        IWindowsServiceController controller,
        ISettingsStore settings,
        IAuditLogger audit,
        TimeProvider clock,
        ILogger<OneCAutoRestartJob> logger)
    {
        _discovery = discovery;
        _controller = controller;
        _settings = settings;
        _audit = audit;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // Защита от рассинхрона: расписание выключено → no-op (задание могло остаться в
        // сторадже между выключением и снятием). Читаем авторитетно из store, не из snapshot.
        var enabled = await _settings.GetIntAsync(SettingKey.OneCAutoRestartEnabled, ct).ConfigureAwait(false) == 1;
        if (!enabled)
        {
            LogSkippedDisabled(_logger);
            return;
        }

        // Рестартим только ЗАПУЩЕННЫЕ службы ragent: остановленный сервер 1С трогать незачем
        // (оператор намеренно остановил — не поднимаем профилактикой).
        var running = _discovery.Discover().Where(s => s.Running).ToList();
        if (running.Count == 0)
        {
            LogNoRunningServers(_logger);
            // Прогон состоялся (расписание сработало) — отметим время, даже если рестартить
            // было нечего: это валидный «прошлый прогон».
            await MarkLastRunAsync(ct).ConfigureAwait(false);
            return;
        }

        var restarted = new List<string>(running.Count);
        foreach (var server in running)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _controller.RestartAsync(server.ServiceName, ct).ConfigureAwait(false);
                restarted.Add(server.ServiceName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (WindowsServiceOperationException ex)
            {
                // Одна сломанная служба не валит весь прогон — логируем и идём дальше.
                LogServiceRestartFailed(_logger, server.ServiceName, ex);
            }
        }

        if (restarted.Count > 0)
        {
            var description = string.Format(
                CultureInfo.InvariantCulture,
                "Авто-рестарт сервера 1С: перезапущены службы {0}.",
                string.Join(", ", restarted));
            await _audit.LogAsync(
                AuditActionType.OneCServerAutoRestarted,
                initiator: SystemInitiator,
                description: description,
                tenantId: null,
                ct: ct).ConfigureAwait(false);
        }

        await MarkLastRunAsync(ct).ConfigureAwait(false);
    }

    // Отметка «прошлого прогона»: UTC, инвариантный ISO-8601 (round-trip "O"). Пишет от имени
    // системы; SetAsync инвалидирует snapshot, поэтому get-эндпоинт увидит свежее значение.
    private Task MarkLastRunAsync(CancellationToken ct)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        return _settings.SetAsync(
            SettingKey.OneCAutoRestartLastRunUtc,
            nowUtc.ToString("O", CultureInfo.InvariantCulture),
            isSecret: false,
            updatedBy: SystemInitiator,
            ct: ct);
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Авто-рестарт сервера 1С пропущен: расписание выключено (OneC.AutoRestart.Enabled=0).")]
    private static partial void LogSkippedDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Авто-рестарт сервера 1С: запущенных служб ragent не обнаружено — рестартить нечего.")]
    private static partial void LogNoRunningServers(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Авто-рестарт сервера 1С: служба «{ServiceName}» не перезапущена — пропущена, остальные продолжены.")]
    private static partial void LogServiceRestartFailed(ILogger logger, string serviceName, Exception ex);
}
