using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Infrastructure.Ras;

namespace MitLicenseCenter.Infrastructure.Server;

// Универсальный надёжный контроллер службы Windows (ADR-55, MLC-212). Обобщает
// RAS-паттерн (ScRasServiceManager) на любую службу узла: команда (sc start/sc stop)
// + ВЕРИФИКАЦИЯ фактического состояния опросом ServiceController до целевого в пределах
// таймаута. Возвращает достоверный итог, а не факт отправки команды. Идемпотентность —
// sc-коды 1056/1062 = успех. Мутации одной службы сериализуются IServiceOperationGate
// (per-service-name). Команды sc собираются raw-строкой как в RasServiceCommandBuilder
// (имя службы — позиционный аргумент, не «ключ= значение»). Эндпоинты/FE — MLC-213+.
internal sealed partial class WindowsServiceController : IWindowsServiceController
{
    // Коды возврата sc.exe (Win32), трактуемые как идемпотентный успех — те же
    // константы, что в ScRasServiceManager.
    private const int ScSuccess = 0;
    private const int ScErrorServiceAlreadyRunning = 1056; // ERROR_SERVICE_ALREADY_RUNNING
    private const int ScErrorServiceNotActive = 1062;      // ERROR_SERVICE_NOT_ACTIVE

    private readonly IScProcessRunner _sc;
    private readonly IServiceStateReader _serviceState;
    private readonly IServiceOperationGate _gate;
    private readonly TimeProvider _clock;
    private readonly WindowsServiceControllerOptions _options;
    private readonly ILogger<WindowsServiceController> _logger;

    public WindowsServiceController(
        IScProcessRunner sc,
        IServiceStateReader serviceState,
        IServiceOperationGate gate,
        TimeProvider clock,
        WindowsServiceControllerOptions options,
        ILogger<WindowsServiceController> logger)
    {
        _sc = sc;
        _serviceState = serviceState;
        _gate = gate;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public async Task<WindowsServiceOperationResult> StartAsync(string serviceName, CancellationToken ct)
    {
        using (await _gate.AcquireAsync(serviceName, ct).ConfigureAwait(false))
        {
            await StartCoreAsync(serviceName, ct).ConfigureAwait(false);
        }

        return new WindowsServiceOperationResult(serviceName, WindowsServiceStatus.Running);
    }

    public async Task<WindowsServiceOperationResult> StopAsync(string serviceName, CancellationToken ct)
    {
        using (await _gate.AcquireAsync(serviceName, ct).ConfigureAwait(false))
        {
            await StopCoreAsync(serviceName, ct).ConfigureAwait(false);
        }

        return new WindowsServiceOperationResult(serviceName, WindowsServiceStatus.Stopped);
    }

    public async Task<WindowsServiceOperationResult> RestartAsync(string serviceName, CancellationToken ct)
    {
        // Стоп и старт — под ОДНИМ захватом замка (между ними не отпускаем): иначе
        // чужая операция могла бы вклиниться. Внутренние Core-шаги замок не берут —
        // повторный Acquire того же ключа в этом потоке = дедлок (SemaphoreSlim не
        // реентерабелен).
        using (await _gate.AcquireAsync(serviceName, ct).ConfigureAwait(false))
        {
            await StopCoreAsync(serviceName, ct).ConfigureAwait(false);
            await StartCoreAsync(serviceName, ct).ConfigureAwait(false);
        }

        return new WindowsServiceOperationResult(serviceName, WindowsServiceStatus.Running);
    }

    // ── Внутренние верифицированные шаги (без захвата замка) ────────────────────────────

    // sc start + опрос до IsRunning=true. Код 1056 (ALREADY_RUNNING) — идемпотентный успех.
    private async Task StartCoreAsync(string serviceName, CancellationToken ct)
    {
        var result = await _sc.RunAsync($"start \"{serviceName}\"", ct).ConfigureAwait(false);
        if (result.ExitCode is not (ScSuccess or ScErrorServiceAlreadyRunning))
        {
            throw new WindowsServiceOperationException(
                $"Не удалось запустить службу «{serviceName}» (sc вернул код {result.ExitCode}).");
        }

        await WaitForStateAsync(serviceName, targetRunning: true, ct).ConfigureAwait(false);
    }

    // sc stop + опрос до IsRunning=false. Код 1062 (NOT_ACTIVE) — идемпотентный успех.
    private async Task StopCoreAsync(string serviceName, CancellationToken ct)
    {
        var result = await _sc.RunAsync($"stop \"{serviceName}\"", ct).ConfigureAwait(false);
        if (result.ExitCode is not (ScSuccess or ScErrorServiceNotActive))
        {
            throw new WindowsServiceOperationException(
                $"Не удалось остановить службу «{serviceName}» (sc вернул код {result.ExitCode}).");
        }

        await WaitForStateAsync(serviceName, targetRunning: false, ct).ConfigureAwait(false);
    }

    // Полинг фактического состояния службы до целевого в пределах VerificationTimeout.
    // Это НОВЫЙ код контракта надёжности (ADR-55) — в RAS-адаптере опроса-до-состояния
    // нет. Достигли цели → возврат. Истёк таймаут → управляемое доменное исключение
    // (позже → 409). Служба исчезла (ReadState=null) → исключение «не найдена».
    private async Task WaitForStateAsync(string serviceName, bool targetRunning, CancellationToken ct)
    {
        var deadline = _clock.GetUtcNow() + _options.VerificationTimeout;

        while (true)
        {
            var state = _serviceState.ReadState(serviceName);
            if (state is null)
            {
                throw new WindowsServiceOperationException(
                    $"Служба «{serviceName}» не найдена на этом узле.");
            }

            if (state.Value.IsRunning == targetRunning)
            {
                return;
            }

            if (_clock.GetUtcNow() >= deadline)
            {
                var target = targetRunning ? "запуститься" : "остановиться";
                LogVerificationTimeout(_logger, serviceName, target, _options.VerificationTimeout.TotalSeconds);
                throw new WindowsServiceOperationException(
                    $"Служба «{serviceName}» не успела {target} за {_options.VerificationTimeout.TotalSeconds:0} с.");
            }

            await Task.Delay(_options.PollInterval, _clock, ct).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Служба «{ServiceName}» не успела {Target} за {TimeoutSeconds:0} с — верификация состояния не достигла цели.")]
    private static partial void LogVerificationTimeout(ILogger logger, string serviceName, string target, double timeoutSeconds);
}
