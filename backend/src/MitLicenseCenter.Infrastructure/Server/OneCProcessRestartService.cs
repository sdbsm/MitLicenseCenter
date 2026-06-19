using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Server;

namespace MitLicenseCenter.Infrastructure.Server;

// Рестарт рабочего процесса 1С (rphost) мягким способом по Pid (MLC-220, ADR-56).
// У rac НЕТ команды «restart process» — рестарт = завершение ОС-процесса rphost по Pid,
// после чего кластер 1С авто-поднимает новый процесс с другим Pid.
//
// Контракт безопасности (строго, операция разрушительна — рвёт активные сеансы на процессе):
//   1. Whitelist: завершаем ТОЛЬКО Pid, присутствующий в текущем `rac process list`
//      (IClusterClient.ListProcessesAsync → OneCProcessLoad.Pid). Нет в списке → NotInCluster
//      (404), НЕ убиваем произвольный ОС-процесс.
//   2. Guard от переиспользования Pid: перед kill проверяем, что ОС-процесс с этим Pid —
//      действительно "rphost" (ILocalProcessTerminator.GetProcessName). Имя не совпало →
//      PidReused (409). Процесса нет (имя null) → процесс уже ушёл = идемпотентный успех.
//   3. Верификация (дух ADR-55; rphost — не служба, ServiceController неприменим): после kill
//      опрашиваем `rac process list` до исчезновения старого Pid в пределах таймаута. Исчез →
//      Restarted (правдивый результат). Не исчез за таймаут → VerificationTimedOut (409).
//   4. Идемпотентность: если Pid уже отсутствует в whitelist ИЛИ ОС-процесс уже ушёл — успех.
//
// Чистая классификация исхода вынесена в OneCProcessRestartPolicy (юнит-тесты). Здесь —
// оркестровка ввода-вывода (rac-снимок + ОС-kill + полинг через TimeProvider).
internal sealed partial class OneCProcessRestartService : IOneCProcessRestartService
{
    // Имя ОС-процесса рабочего процесса 1С — без расширения (как Process.ProcessName).
    private const string RphostProcessName = "rphost";

    private readonly IClusterClient _cluster;
    private readonly ILocalProcessTerminator _terminator;
    private readonly TimeProvider _clock;
    private readonly OneCProcessRestartOptions _options;
    private readonly ILogger<OneCProcessRestartService> _logger;

    public OneCProcessRestartService(
        IClusterClient cluster,
        ILocalProcessTerminator terminator,
        TimeProvider clock,
        OneCProcessRestartOptions options,
        ILogger<OneCProcessRestartService> logger)
    {
        _cluster = cluster;
        _terminator = terminator;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public async Task<OneCProcessRestartResult> RestartAsync(int pid, CancellationToken ct)
    {
        // (1) Whitelist: Pid обязан быть в текущем снимке rac process list.
        var inCluster = await IsPidInClusterAsync(pid, ct).ConfigureAwait(false);
        if (!inCluster)
        {
            // Не в кластере — НЕ убиваем. (Возможные причины: чужой Pid, либо процесс уже
            // ушёл сам. По безопасности обе → отказ whitelist; для пользователя 404.)
            return new OneCProcessRestartResult(OneCProcessRestartOutcome.NotInCluster, pid);
        }

        // (2) Guard от переиспользования Pid: ОС-процесс должен быть именно rphost.
        var osName = _terminator.GetProcessName(pid);
        var classification = OneCProcessRestartPolicy.ClassifyBeforeKill(osName, RphostProcessName);
        if (classification is OneCProcessRestartOutcome outcomeBeforeKill)
        {
            // PidReused (имя не rphost) → 409; либо процесс уже ушёл (имя null) →
            // идемпотентный успех. В обоих случаях kill НЕ выполняется.
            if (outcomeBeforeKill == OneCProcessRestartOutcome.PidReused)
            {
                LogPidReused(_logger, pid, osName);
            }

            return new OneCProcessRestartResult(outcomeBeforeKill, pid);
        }

        // Имя совпало с rphost — завершаем. Kill=false (исчез между проверкой и kill) —
        // тоже идемпотентный успех, верификацию всё равно проводим (Pid должен исчезнуть).
        _terminator.Kill(pid);

        // (3) Верификация: опрашиваем rac process list до исчезновения старого Pid.
        var gone = await WaitForPidGoneAsync(pid, ct).ConfigureAwait(false);
        if (gone)
        {
            return new OneCProcessRestartResult(OneCProcessRestartOutcome.Restarted, pid);
        }

        LogVerificationTimeout(_logger, pid, _options.VerificationTimeout.TotalSeconds);
        return new OneCProcessRestartResult(OneCProcessRestartOutcome.VerificationTimedOut, pid);
    }

    // Pid присутствует в текущем rac process list? Снимок «never throws» (rac недоступен →
    // пустой список → Pid не найден → whitelist не пройден, это корректный отказ).
    private async Task<bool> IsPidInClusterAsync(int pid, CancellationToken ct)
    {
        var processes = await _cluster.ListProcessesAsync(ct).ConfigureAwait(false);
        return processes.Any(p => p.Pid == pid);
    }

    // Полинг rac process list до исчезновения Pid в пределах VerificationTimeout. Дух
    // ADR-55 (правдивый результат, ограниченное ожидание): исчез → true; истёк таймаут → false.
    private async Task<bool> WaitForPidGoneAsync(int pid, CancellationToken ct)
    {
        var deadline = _clock.GetUtcNow() + _options.VerificationTimeout;

        while (true)
        {
            if (!await IsPidInClusterAsync(pid, ct).ConfigureAwait(false))
            {
                return true;
            }

            if (_clock.GetUtcNow() >= deadline)
            {
                return false;
            }

            await Task.Delay(_options.PollInterval, _clock, ct).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Рестарт rphost: Pid {Pid} переиспользован ОС (процесс «{OsName}», не rphost) — завершение отменено.")]
    private static partial void LogPidReused(ILogger logger, int pid, string? osName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Рестарт rphost: Pid {Pid} не исчез из rac process list за {TimeoutSeconds:0} с — верификация не достигнута.")]
    private static partial void LogVerificationTimeout(ILogger logger, int pid, double timeoutSeconds);
}
