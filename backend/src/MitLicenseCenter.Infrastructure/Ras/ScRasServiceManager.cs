using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Ras;

// Адаптер управления локальной службой RAS поверх sc.exe (ADR-47). Обнаружение — по
// binPath, содержащему ras.exe (имя службы не стандартизировано). Диагностика — 4
// состояния. register/update/start — sc create / (stop→config→start) / start. Хост
// фиксирован localhost (single-host, ADR-28); порт — OneC.RAS.Endpoint; платформа —
// OneC.DefaultPlatformVersion.
internal sealed partial class ScRasServiceManager : IRasServiceManager
{
    // Стандартный порт самой службы RAS, если в OneC.RAS.Endpoint порт не задан.
    private const string DefaultRasPort = "1545";

    // Адрес локального агента кластера ragent: single-host → loopback, 1540 —
    // стандартный порт агента кластера 1С. Цель ras.exe (последний позиционный аргумент).
    private const string LocalAgentAddress = "localhost:1540";

    // Аргументы перечисления всех служб (вынесено в поле — CA1861).
    private static readonly string[] QueryAllServicesArgs = { "query", "state=", "all" };

    // Коды возврата sc.exe (Win32), которые нужно трактовать особо.
    private const int ScSuccess = 0;
    private const int ScErrorServiceAlreadyRunning = 1056; // ERROR_SERVICE_ALREADY_RUNNING
    private const int ScErrorServiceNotActive = 1062;      // ERROR_SERVICE_NOT_ACTIVE
    private const int ScErrorServiceDoesNotExist = 1060;   // ERROR_SERVICE_DOES_NOT_EXIST
    private const int ScErrorServiceExists = 1073;         // ERROR_SERVICE_EXISTS

    private readonly IScProcessRunner _sc;
    private readonly ISettingsSnapshot _settings;
    private readonly IRasExePathResolver _rasResolver;
    private readonly ILogger<ScRasServiceManager> _logger;

    public ScRasServiceManager(
        IScProcessRunner sc,
        ISettingsSnapshot settings,
        IRasExePathResolver rasResolver,
        ILogger<ScRasServiceManager> logger)
    {
        _sc = sc;
        _settings = settings;
        _rasResolver = rasResolver;
        _logger = logger;
    }

    // ── Диагностика ────────────────────────────────────────────────────────────────────

    public async Task<RasServiceDiagnosis> DiagnoseAsync(CancellationToken ct)
    {
        var desiredPort = ResolveDesiredPort();
        var platformVersion = (_settings.GetString(SettingKey.OneCDefaultPlatformVersion) ?? string.Empty).Trim();
        var rasExePath = string.IsNullOrEmpty(platformVersion)
            ? null
            : _rasResolver.ResolveForVersion(platformVersion);

        var target = rasExePath is not null
            ? new RasServiceTarget(rasExePath, platformVersion, desiredPort, LocalAgentAddress)
            : null;

        var service = await FindRasServiceAsync(ct).ConfigureAwait(false);

        // (2) Службы с ras.exe в binPath нет → предложить регистрацию.
        if (service is null)
        {
            return new RasServiceDiagnosis(
                State: RasServiceState.NotRegistered,
                Service: null,
                Target: target,
                CommandPreview: target is null
                    ? null
                    : RasServiceCommandBuilder.BuildScCreatePreview(
                        RasServiceCommandBuilder.DefaultServiceName, target.RasExePath, target.Port, target.AgentAddress),
                TargetReady: target is not null,
                Issue: target is null ? BuildTargetIssue(platformVersion) : null);
        }

        // (4) Служба есть, но остановлена → предложить запуск.
        if (!service.IsRunning)
        {
            return new RasServiceDiagnosis(
                State: RasServiceState.Stopped,
                Service: service,
                Target: target,
                CommandPreview: RasServiceCommandBuilder.BuildScStartPreview(service.ServiceName),
                TargetReady: true,
                Issue: null);
        }

        // (3) binPath на устаревшей версии ИЛИ порт ≠ endpoint → перерегистрация.
        var versionStale = target is not null
            && !string.IsNullOrEmpty(service.PlatformVersion)
            && !string.Equals(service.PlatformVersion, target.PlatformVersion, StringComparison.OrdinalIgnoreCase);
        var effectivePort = service.Port ?? DefaultRasPort; // нет --port → служба слушает дефолт.
        var portStale = !string.Equals(effectivePort, desiredPort, StringComparison.Ordinal);

        if (versionStale || portStale)
        {
            return new RasServiceDiagnosis(
                State: RasServiceState.Outdated,
                Service: service,
                Target: target,
                CommandPreview: target is null
                    ? null
                    : RasServiceCommandBuilder.BuildScConfigPreview(
                        service.ServiceName, target.RasExePath, target.Port, target.AgentAddress),
                TargetReady: target is not null,
                Issue: target is null ? BuildTargetIssue(platformVersion) : null);
        }

        // (1) OK.
        return new RasServiceDiagnosis(
            State: RasServiceState.Ok,
            Service: service,
            Target: target,
            CommandPreview: null,
            TargetReady: true,
            Issue: null);
    }

    // ── Операции ───────────────────────────────────────────────────────────────────────

    public async Task<RasServiceOperationResult> RegisterAsync(CancellationToken ct)
    {
        var target = await ResolveTargetOrThrowAsync(ct).ConfigureAwait(false);

        // Идемпотентность: если служба с ras.exe уже есть — это не register, а update.
        var existing = await FindRasServiceAsync(ct).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new RasServiceOperationException(
                "Служба RAS уже зарегистрирована. Используйте «Обновить» вместо повторной регистрации.");
        }

        var name = RasServiceCommandBuilder.DefaultServiceName;
        var create = await _sc.RunAsync(
            RasServiceCommandBuilder.BuildScCreateArguments(name, target.RasExePath, target.Port, target.AgentAddress),
            ct).ConfigureAwait(false);

        if (create.ExitCode == ScErrorServiceExists)
        {
            // Служба с нашим именем уже есть (имя ras.exe в её binPath не было → не нашли
            // как RAS-службу). Выравниваем её под текущий target через config.
            await ConfigureAndStartAsync(name, target, ct).ConfigureAwait(false);
            return new RasServiceOperationResult(RasServiceState.Ok, name, target.PlatformVersion, target.Port);
        }

        ThrowIfScFailed(create, "создать", name);

        var start = await _sc.RunAsync(RasServiceCommandBuilder.BuildScStartArguments(name), ct).ConfigureAwait(false);
        ThrowIfStartFailed(start, name);

        return new RasServiceOperationResult(RasServiceState.Ok, name, target.PlatformVersion, target.Port);
    }

    public async Task<RasServiceOperationResult> UpdateAsync(CancellationToken ct)
    {
        var target = await ResolveTargetOrThrowAsync(ct).ConfigureAwait(false);

        var existing = await FindRasServiceAsync(ct).ConfigureAwait(false)
            ?? throw new RasServiceOperationException(
                "Служба RAS не найдена. Сначала зарегистрируйте её.");

        await ConfigureAndStartAsync(existing.ServiceName, target, ct).ConfigureAwait(false);
        return new RasServiceOperationResult(RasServiceState.Ok, existing.ServiceName, target.PlatformVersion, target.Port);
    }

    public async Task<RasServiceOperationResult> StartAsync(CancellationToken ct)
    {
        var existing = await FindRasServiceAsync(ct).ConfigureAwait(false)
            ?? throw new RasServiceOperationException(
                "Служба RAS не найдена. Сначала зарегистрируйте её.");

        var start = await _sc.RunAsync(
            RasServiceCommandBuilder.BuildScStartArguments(existing.ServiceName), ct).ConfigureAwait(false);
        ThrowIfStartFailed(start, existing.ServiceName);

        // Start не перенастраивает службу — платформа/порт берём из обнаруженной службы как есть.
        return new RasServiceOperationResult(
            RasServiceState.Ok, existing.ServiceName, existing.PlatformVersion, existing.Port);
    }

    // stop (мягко, игнор «уже остановлена») → config (новый binPath/порт) → start.
    private async Task ConfigureAndStartAsync(string name, RasServiceTarget target, CancellationToken ct)
    {
        // Остановка перед сменой binPath: на запущенной службе config применится, но
        // ras.exe продолжит крутиться со старыми аргументами до перезапуска. «Не активна»
        // (1062) — не ошибка (служба и так стояла).
        var stop = await _sc.RunAsync(RasServiceCommandBuilder.BuildScStopArguments(name), ct).ConfigureAwait(false);
        if (stop.ExitCode is not (ScSuccess or ScErrorServiceNotActive))
        {
            LogScStopWarning(_logger, name, stop.ExitCode);
        }

        var config = await _sc.RunAsync(
            RasServiceCommandBuilder.BuildScConfigArguments(name, target.RasExePath, target.Port, target.AgentAddress),
            ct).ConfigureAwait(false);
        ThrowIfScFailed(config, "перенастроить", name);

        var start = await _sc.RunAsync(RasServiceCommandBuilder.BuildScStartArguments(name), ct).ConfigureAwait(false);
        ThrowIfStartFailed(start, name);
    }

    // ── Обнаружение ──────────────────────────────────────────────────────────────────────

    // Перечисляем все службы и ищем ПЕРВУЮ, чей binPath (sc qc) содержит ras.exe.
    private async Task<DiscoveredRasService?> FindRasServiceAsync(CancellationToken ct)
    {
        var query = await _sc.RunAsync(QueryAllServicesArgs, ct).ConfigureAwait(false);
        if (query.ExitCode != ScSuccess)
        {
            throw new RasServiceOperationException(
                "Не удалось получить список служб Windows (sc query).");
        }

        foreach (var name in ScOutputParser.ParseServiceNames(query.Stdout))
        {
            ct.ThrowIfCancellationRequested();

            var qc = await _sc.RunAsync(new[] { "qc", name }, ct).ConfigureAwait(false);
            if (qc.ExitCode != ScSuccess)
            {
                continue; // служба исчезла между enum и qc / нет прав на конкретную — пропускаем.
            }

            var binPath = ScOutputParser.ParseBinaryPath(qc.Stdout);
            if (!ScOutputParser.BinPathReferencesRas(binPath))
            {
                continue;
            }

            var status = await _sc.RunAsync(new[] { "query", name }, ct).ConfigureAwait(false);
            var isRunning = status.ExitCode == ScSuccess && ScOutputParser.ParseIsRunning(status.Stdout);

            return new DiscoveredRasService(
                ServiceName: name,
                IsRunning: isRunning,
                BinPath: binPath,
                PlatformVersion: ScOutputParser.ParsePlatformVersion(binPath),
                Port: ScOutputParser.ParsePort(binPath));
        }

        return null;
    }

    // ── Вспомогательное ──────────────────────────────────────────────────────────────────

    // Порт службы RAS из OneC.RAS.Endpoint (формат host:port). Берём только порт (хост —
    // всегда localhost, single-host). Пустой/без порта → дефолт 1545.
    private string ResolveDesiredPort()
    {
        var endpoint = (_settings.GetString(SettingKey.OneCRasEndpoint) ?? string.Empty).Trim();
        if (endpoint.Length == 0)
        {
            return DefaultRasPort;
        }

        var colon = endpoint.LastIndexOf(':');
        if (colon < 0 || colon == endpoint.Length - 1)
        {
            return DefaultRasPort;
        }

        var port = endpoint[(colon + 1)..].Trim();
        return port.Length > 0 && port.All(char.IsDigit) ? port : DefaultRasPort;
    }

    private async Task<RasServiceTarget> ResolveTargetOrThrowAsync(CancellationToken ct)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var platformVersion = (_settings.GetString(SettingKey.OneCDefaultPlatformVersion) ?? string.Empty).Trim();
        if (platformVersion.Length == 0)
        {
            throw new RasServiceOperationException(
                "Не задана версия платформы по умолчанию (OneC.DefaultPlatformVersion). Укажите её в «Параметрах».");
        }

        var rasExePath = _rasResolver.ResolveForVersion(platformVersion)
            ?? throw new RasServiceOperationException(
                $"Не найден ras.exe платформы {platformVersion}. Проверьте установку 1С выбранной версии.");

        return new RasServiceTarget(rasExePath, platformVersion, ResolveDesiredPort(), LocalAgentAddress);
    }

    private static string BuildTargetIssue(string platformVersion)
        => platformVersion.Length == 0
            ? "Не задана версия платформы по умолчанию — выберите её в «Параметрах», чтобы зарегистрировать службу."
            : $"Не найден ras.exe платформы {platformVersion}. Проверьте установку 1С выбранной версии.";

    private static void ThrowIfScFailed(ScResult result, string verb, string name)
    {
        if (result.ExitCode != ScSuccess)
        {
            throw new RasServiceOperationException(
                $"Не удалось {verb} службу RAS «{name}» (sc вернул код {result.ExitCode}).");
        }
    }

    // sc start: код 1056 (ALREADY_RUNNING) — успех (служба уже работает, MLC-116-прецедент).
    private static void ThrowIfStartFailed(ScResult result, string name)
    {
        if (result.ExitCode is ScSuccess or ScErrorServiceAlreadyRunning)
        {
            return;
        }
        throw new RasServiceOperationException(
            $"Не удалось запустить службу RAS «{name}» (sc вернул код {result.ExitCode}).");
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "sc stop службы RAS «{ServiceName}» вернул код {ExitCode} — перенастройка продолжается.")]
    private static partial void LogScStopWarning(ILogger logger, string serviceName, int exitCode);
}
