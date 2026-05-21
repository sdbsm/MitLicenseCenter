using System.Globalization;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Production RAS-адаптер: оборачивает rac.exe. Активируется decorator'ом
// ResilientClusterClient, когда REST circuit Open. CLI-контракт — ADR-3.3.
// Spawn-cadence: ≤1 process per List/Kill/Ping. Внутри List ещё один process
// (cluster list) для UUID-резолва — итого ≤2 per ListActiveSessionsAsync,
// что в рамках memory-правила «не спавнить rac.exe per polling tick».
internal sealed partial class RacExecutableRasClusterClient : IRasFallbackClusterClient
{
    // Идемпотентный маркер «session not found» — стабильное русское сообщение
    // 1С Cluster Administration Server. Substring-проверка устойчива к
    // possible локальным изменениям типа CRLF/окончания фразы.
    private const string SessionNotFoundMarker = "Сеанс с указанным идентификатором не найден";

    // Лимит на один rac.exe вызов: rac→ras→ragent дольше REST hop'а,
    // 30s оставляет запас на cold-кластер и сетевую задержку.
    private static readonly TimeSpan InvocationTimeout = TimeSpan.FromSeconds(30);

    // Тот же список app-id, что в REST-адаптере (PR 3.2). Source of truth
    // зафиксирован в ADR-3.1/3.3; см. также memory/domain_definitions.md.
    private static readonly HashSet<string> LicenseConsumingAppIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "1CV8", "1CV8C", "WebClient", "Designer", "COMConnection",
        };

    private readonly IRacProcessRunner _runner;
    private readonly ISettingsSnapshot _settings;
    private readonly ILogger<RacExecutableRasClusterClient> _logger;

    public RacExecutableRasClusterClient(
        IRacProcessRunner runner,
        ISettingsSnapshot settings,
        ILogger<RacExecutableRasClusterClient> logger)
    {
        _runner = runner;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct)
    {
        if (!TryGetExePath(out var exePath))
        {
            return Array.Empty<ClusterSession>();
        }

        var clusterUuid = await ResolveClusterUuidAsync(exePath, ct).ConfigureAwait(false);
        if (clusterUuid is null)
        {
            return Array.Empty<ClusterSession>();
        }

        var args = BuildArgsWithAuth(
            "session", "list",
            $"--cluster={clusterUuid}");

        var invocation = await _runner.RunAsync(exePath, args, InvocationTimeout, ct)
            .ConfigureAwait(false);

        if (invocation.ExitCode != 0)
        {
            LogRacFailed(_logger, "session list", invocation.ExitCode, invocation.Stderr.Trim());
            return Array.Empty<ClusterSession>();
        }

        var records = RacOutputParser.Parse(invocation.Stdout);
        var sessions = new List<ClusterSession>(records.Count);

        foreach (var rec in records)
        {
            if (!TryParseSession(rec, out var session))
            {
                continue;
            }
            sessions.Add(session);
        }

        return sessions;
    }

    public async Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct)
    {
        if (!TryGetExePath(out var exePath))
        {
            return new KillSessionResult(Killed: false, AlreadyGone: false);
        }

        var clusterUuid = await ResolveClusterUuidAsync(exePath, ct).ConfigureAwait(false);
        if (clusterUuid is null)
        {
            return new KillSessionResult(Killed: false, AlreadyGone: false);
        }

        var args = BuildArgsWithAuth(
            "session", "terminate",
            $"--cluster={clusterUuid}",
            $"--session={descriptor.SessionId:D}");

        var invocation = await _runner.RunAsync(exePath, args, InvocationTimeout, ct)
            .ConfigureAwait(false);

        if (invocation.ExitCode == 0)
        {
            return new KillSessionResult(Killed: true, AlreadyGone: false);
        }

        // Идемпотентный no-op: сеанс уже завершился между snapshot и kill.
        // Семантика совпадает с REST 404 (см. ADR-3.1/3.3, infrastructure_integration.md).
        if (invocation.Stderr.Contains(SessionNotFoundMarker, StringComparison.Ordinal))
        {
            return new KillSessionResult(Killed: false, AlreadyGone: true);
        }

        LogRacFailed(_logger, "session terminate", invocation.ExitCode, invocation.Stderr.Trim());
        return new KillSessionResult(Killed: false, AlreadyGone: false);
    }

    public async Task<ClusterPingResult> PingAsync(CancellationToken ct)
    {
        if (!TryGetExePath(out var exePath))
        {
            return new ClusterPingResult(Ok: false, Error: "OneC.RAS.ExePath не задан.");
        }

        var args = BuildArgs("cluster", "list");

        var invocation = await _runner.RunAsync(exePath, args, InvocationTimeout, ct)
            .ConfigureAwait(false);

        if (invocation.ExitCode == 0)
        {
            return new ClusterPingResult(Ok: true, Error: null);
        }

        return new ClusterPingResult(Ok: false, Error: invocation.Stderr.Trim());
    }

    // --- Internals ---

    private async Task<string?> ResolveClusterUuidAsync(string exePath, CancellationToken ct)
    {
        var args = BuildArgs("cluster", "list");
        var invocation = await _runner.RunAsync(exePath, args, InvocationTimeout, ct)
            .ConfigureAwait(false);

        if (invocation.ExitCode != 0)
        {
            LogRacFailed(_logger, "cluster list", invocation.ExitCode, invocation.Stderr.Trim());
            return null;
        }

        var records = RacOutputParser.Parse(invocation.Stdout);
        if (records.Count == 0)
        {
            return null;
        }

        return records[0].TryGetValue("cluster", out var uuid) && !string.IsNullOrWhiteSpace(uuid)
            ? uuid
            : null;
    }

    private bool TryGetExePath(out string exePath)
    {
        exePath = _settings.GetString(SettingKey.OneCRasExePath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            LogExePathMissing(_logger);
            return false;
        }
        return true;
    }

    // Endpoint + auth-флаги — общая часть всех команд.
    // Endpoint всегда передаётся явно (если задан в Settings), чтобы misconfig
    // surface'ил как connection error, а не silently на localhost.
    // Auth-флаги опускаются если creds пустые (cluster без зарегистрированных
    // админов работает анонимно — как локальный тест-rig).
    private List<string> BuildArgs(params string[] commandAndOptions)
    {
        var args = new List<string>(capacity: commandAndOptions.Length + 1);

        var endpoint = _settings.GetString(SettingKey.OneCRasEndpoint);
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            args.Add(endpoint);
        }

        args.AddRange(commandAndOptions);
        return args;
    }

    private List<string> BuildArgsWithAuth(params string[] commandAndOptions)
    {
        var args = BuildArgs(commandAndOptions);

        var user = _settings.GetString(SettingKey.OneCClusterAdminUser);
        var password = _settings.GetString(SettingKey.OneCClusterAdminPassword);
        if (!string.IsNullOrWhiteSpace(user) && password is not null)
        {
            args.Add($"--cluster-user={user}");
            args.Add($"--cluster-pwd={password}");
        }

        return args;
    }

    private static bool TryParseSession(IReadOnlyDictionary<string, string> rec, out ClusterSession session)
    {
        session = default!;

        if (!rec.TryGetValue("session", out var sessionRaw)
            || !Guid.TryParse(sessionRaw, out var sessionId))
        {
            return false;
        }
        if (!rec.TryGetValue("infobase", out var infobaseRaw)
            || !Guid.TryParse(infobaseRaw, out var infobaseId))
        {
            return false;
        }

        var appId = rec.GetValueOrDefault("app-id") ?? string.Empty;

        session = new ClusterSession(
            SessionId: sessionId,
            ClusterInfobaseId: infobaseId,
            AppId: appId,
            UserName: rec.GetValueOrDefault("user-name") ?? string.Empty,
            Host: rec.GetValueOrDefault("host") ?? string.Empty,
            ConsumesLicense: LicenseConsumingAppIds.Contains(appId),
            StartedAtUtc: ParseUtc(rec.GetValueOrDefault("started-at")));

        return true;
    }

    private static DateTime ParseUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DateTime.UtcNow;
        }
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
        return DateTime.UtcNow;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "rac.exe {Command} вернул exit={ExitCode}, stderr={Stderr}")]
    private static partial void LogRacFailed(ILogger logger, string command, int exitCode, string stderr);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Параметр OneC.RAS.ExePath не задан — RAS-адаптер не может выполнить запрос.")]
    private static partial void LogExePathMissing(ILogger logger);
}
