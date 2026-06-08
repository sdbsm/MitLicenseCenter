using System.Globalization;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Production RAS-адаптер: оборачивает rac.exe. Единственный 1С cluster-адаптер
// после Stage 5 PR 5.1 (ADR-16) — REST primary удалён, decorator'а больше нет.
// CLI-контракт — ADR-3.3.
// Spawn-cadence (MLC-041): UUID кластера кэшируется между вызовами в IClusterUuidCache
// (singleton). При тёплом кэше «cluster list» НЕ спавнится — ListActiveSessionsAsync = 1
// process (session list), Kill = 1 (terminate). Резолв «cluster list» происходит только
// на холодном кэше / смене endpoint / инвалидации после ошибки команды — итого ≤2 на
// ListActiveSessionsAsync в худшем случае. В рамках memory-правила «не спавнить rac.exe
// per polling tick».
internal sealed partial class RacExecutableRasClusterClient : IClusterClient
{
    // Идемпотентный маркер «session not found» — стабильное русское сообщение
    // 1С Cluster Administration Server. Substring-проверка устойчива к
    // possible локальным изменениям типа CRLF/окончания фразы.
    private const string SessionNotFoundMarker = "Сеанс с указанным идентификатором не найден";

    // Лимит на один rac.exe вызов: rac→ras→ragent дольше REST hop'а,
    // 30s оставляет запас на cold-кластер и сетевую задержку.
    private static readonly TimeSpan InvocationTimeout = TimeSpan.FromSeconds(30);

    private readonly IRacProcessRunner _runner;
    private readonly ISettingsSnapshot _settings;
    private readonly IClusterUuidCache _uuidCache;
    private readonly ILogger<RacExecutableRasClusterClient> _logger;

    public RacExecutableRasClusterClient(
        IRacProcessRunner runner,
        ISettingsSnapshot settings,
        IClusterUuidCache uuidCache,
        ILogger<RacExecutableRasClusterClient> logger)
    {
        _runner = runner;
        _settings = settings;
        _uuidCache = uuidCache;
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
            // Safety-net: ошибка cluster-scoped команды могла означать stale UUID —
            // сбрасываем кэш, следующий вызов перерезолвит (MLC-041).
            _uuidCache.Invalidate(BuildClusterKey(exePath));
            return Array.Empty<ClusterSession>();
        }

        // Whitelist лицензионных app-id читаем один раз на вызов (не per-session) через
        // тот же TTL-кэш SettingsSnapshot, что и ExePath/Endpoint. Пусто/незадано → дефолт.
        var licenseAppIds = LicenseConsumingAppIds.Parse(
            _settings.GetString(SettingKey.OneCLicenseConsumingAppIds));

        var records = RacOutputParser.Parse(invocation.Stdout);
        var sessions = new List<ClusterSession>(records.Count);

        foreach (var rec in records)
        {
            if (!TryParseSession(rec, licenseAppIds, out var session))
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
        // Это успешный no-op, НЕ ошибка кластера → кэш UUID не инвалидируем.
        if (invocation.Stderr.Contains(SessionNotFoundMarker, StringComparison.Ordinal))
        {
            return new KillSessionResult(Killed: false, AlreadyGone: true);
        }

        LogRacFailed(_logger, "session terminate", invocation.ExitCode, invocation.Stderr.Trim());
        // Safety-net: прочая ошибка terminate могла означать stale UUID (MLC-041).
        _uuidCache.Invalidate(BuildClusterKey(exePath));
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

    public async Task<ClusterInfobaseDiscoveryResult> ListInfobasesAsync(CancellationToken ct)
    {
        if (!TryGetExePath(out var exePath))
        {
            return new ClusterInfobaseDiscoveryResult(
                Array.Empty<ClusterInfobase>(), Available: false, Error: "OneC.RAS.ExePath не задан.");
        }

        var clusterUuid = await ResolveClusterUuidAsync(exePath, ct).ConfigureAwait(false);
        if (clusterUuid is null)
        {
            return new ClusterInfobaseDiscoveryResult(
                Array.Empty<ClusterInfobase>(), Available: false,
                Error: "Не удалось получить список кластеров (rac.exe cluster list).");
        }

        var args = BuildArgsWithAuth(
            "infobase", "summary", "list",
            $"--cluster={clusterUuid}");

        var invocation = await _runner.RunAsync(exePath, args, InvocationTimeout, ct)
            .ConfigureAwait(false);

        if (invocation.ExitCode != 0)
        {
            LogRacFailed(_logger, "infobase summary list", invocation.ExitCode, invocation.Stderr.Trim());
            // Safety-net: ошибка cluster-scoped команды могла означать stale UUID (MLC-041).
            _uuidCache.Invalidate(BuildClusterKey(exePath));
            return new ClusterInfobaseDiscoveryResult(
                Array.Empty<ClusterInfobase>(), Available: false, Error: invocation.Stderr.Trim());
        }

        var infobases = ParseInfobases(invocation.Stdout);
        return new ClusterInfobaseDiscoveryResult(infobases, Available: true, Error: null);
    }

    // Раздел «Быстродействие» (MLC-066): тот же спавн `session list`, что и kill-путь, но
    // богаче маппинг (perf-поля). Отдельный метод — чтобы не утяжелять ClusterSession,
    // который держит enforcement-путь тонким. Ошибка → пустой список + инвалидация UUID (MLC-041).
    public async Task<IReadOnlyList<OneCSessionLoad>> ListSessionLoadsAsync(CancellationToken ct)
    {
        if (!TryGetExePath(out var exePath))
        {
            return Array.Empty<OneCSessionLoad>();
        }

        var clusterUuid = await ResolveClusterUuidAsync(exePath, ct).ConfigureAwait(false);
        if (clusterUuid is null)
        {
            return Array.Empty<OneCSessionLoad>();
        }

        var args = BuildArgsWithAuth(
            "session", "list",
            $"--cluster={clusterUuid}");

        var invocation = await _runner.RunAsync(exePath, args, InvocationTimeout, ct)
            .ConfigureAwait(false);

        if (invocation.ExitCode != 0)
        {
            LogRacFailed(_logger, "session list", invocation.ExitCode, invocation.Stderr.Trim());
            _uuidCache.Invalidate(BuildClusterKey(exePath));
            return Array.Empty<OneCSessionLoad>();
        }

        return ParseSessionLoads(invocation.Stdout);
    }

    // Раздел «Быстродействие» (MLC-066): рабочие процессы кластера. +1 спавн `rac.exe` на poll
    // сверх session list (spawn-бюджет ADR-3.3) — live-pull по требованию, не фон. UUID берётся
    // из того же кэша, лишний `cluster list` не спавнится при тёплом кэше.
    public async Task<IReadOnlyList<OneCProcessLoad>> ListProcessesAsync(CancellationToken ct)
    {
        if (!TryGetExePath(out var exePath))
        {
            return Array.Empty<OneCProcessLoad>();
        }

        var clusterUuid = await ResolveClusterUuidAsync(exePath, ct).ConfigureAwait(false);
        if (clusterUuid is null)
        {
            return Array.Empty<OneCProcessLoad>();
        }

        var args = BuildArgsWithAuth(
            "process", "list",
            $"--cluster={clusterUuid}");

        var invocation = await _runner.RunAsync(exePath, args, InvocationTimeout, ct)
            .ConfigureAwait(false);

        if (invocation.ExitCode != 0)
        {
            LogRacFailed(_logger, "process list", invocation.ExitCode, invocation.Stderr.Trim());
            _uuidCache.Invalidate(BuildClusterKey(exePath));
            return Array.Empty<OneCProcessLoad>();
        }

        return ParseProcessLoads(invocation.Stdout);
    }

    // Маппинг `rac session list` → OneCSessionLoad с perf-полями (MLC-066). Required: session,
    // infobase (как у kill-маппинга — запись без них пропускается). Все perf-поля опциональны
    // (parser «never throws» — могут отсутствовать на иных версиях). Числа парсятся инвариантно.
    internal static IReadOnlyList<OneCSessionLoad> ParseSessionLoads(string stdout)
    {
        var records = RacOutputParser.Parse(stdout);
        var result = new List<OneCSessionLoad>(records.Count);

        foreach (var rec in records)
        {
            if (!rec.TryGetValue("session", out var sessionRaw) || !Guid.TryParse(sessionRaw, out var sessionId))
            {
                continue;
            }
            if (!rec.TryGetValue("infobase", out var infobaseRaw) || !Guid.TryParse(infobaseRaw, out var infobaseId))
            {
                continue;
            }

            result.Add(new OneCSessionLoad(
                SessionId: sessionId,
                SessionNumber: ParseIntOrNull(rec.GetValueOrDefault("session-id")),
                ClusterInfobaseId: infobaseId,
                AppId: rec.GetValueOrDefault("app-id") ?? string.Empty,
                UserName: rec.GetValueOrDefault("user-name") ?? string.Empty,
                Host: rec.GetValueOrDefault("host") ?? string.Empty,
                Process: ParseGuidOrNull(rec.GetValueOrDefault("process")),
                Connection: ParseGuidOrNull(rec.GetValueOrDefault("connection")),
                CpuTimeCurrent: ParseLongOrNull(rec.GetValueOrDefault("cpu-time-current")),
                DurationCurrent: ParseLongOrNull(rec.GetValueOrDefault("duration-current")),
                DurationCurrentDbms: ParseLongOrNull(rec.GetValueOrDefault("duration-current-dbms")),
                MemoryCurrent: ParseLongOrNull(rec.GetValueOrDefault("memory-current")),
                BlockedByDbms: ParseIntOrNull(rec.GetValueOrDefault("blocked-by-dbms")),
                BlockedByLs: ParseIntOrNull(rec.GetValueOrDefault("blocked-by-ls")),
                LastActiveAtUtc: ParseUtcOrNull(rec.GetValueOrDefault("last-active-at"))));
        }

        return result;
    }

    // Маппинг `rac process list` → OneCProcessLoad (MLC-066). Required: process (UUID рабочего
    // процесса). `available-perfomance` — ключ rac с опечаткой (сохраняем как есть). `avg-call-time`
    // дробный → инвариантный парс (научная нотация). memory-size большой → long.
    internal static IReadOnlyList<OneCProcessLoad> ParseProcessLoads(string stdout)
    {
        var records = RacOutputParser.Parse(stdout);
        var result = new List<OneCProcessLoad>(records.Count);

        foreach (var rec in records)
        {
            if (!rec.TryGetValue("process", out var processRaw) || !Guid.TryParse(processRaw, out var processId))
            {
                continue;
            }

            result.Add(new OneCProcessLoad(
                Process: processId,
                Pid: ParseIntOrNull(rec.GetValueOrDefault("pid")),
                AvailablePerformance: ParseIntOrNull(rec.GetValueOrDefault("available-perfomance")),
                AvgCallTime: ParseDoubleOrNull(rec.GetValueOrDefault("avg-call-time")),
                MemorySize: ParseLongOrNull(rec.GetValueOrDefault("memory-size"))));
        }

        return result;
    }

    // Инвариантный числовой парс perf-полей (MLC-066). Integer допускает знак — `memory-current`
    // бывает отрицательным (GC, разведка MLC-063). Float допускает экспоненту — `avg-call-time`
    // встречается в научной нотации (`9.99E-05`). Отсутствует/битое → null (поля опциональны).
    private static int? ParseIntOrNull(string? raw)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static long? ParseLongOrNull(string? raw)
        => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? ParseDoubleOrNull(string? raw)
        => double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    // Нулевой UUID (`0000…`) = «не привязан» (сеанс к рабочему процессу / соединению) → null.
    private static Guid? ParseGuidOrNull(string? raw)
        => Guid.TryParse(raw, out var g) && g != Guid.Empty ? g : null;

    // `last-active-at` — ISO без offset, локальное время сервера (как started-at, см. ParseUtc).
    private static DateTime? ParseUtcOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
        return null;
    }

    internal static IReadOnlyList<ClusterInfobase> ParseInfobases(string stdout)
    {
        var records = RacOutputParser.Parse(stdout);
        var result = new List<ClusterInfobase>(records.Count);

        foreach (var rec in records)
        {
            if (!rec.TryGetValue("infobase", out var idRaw) || !Guid.TryParse(idRaw, out var id))
            {
                continue;
            }

            var name = rec.GetValueOrDefault("name");
            if (string.IsNullOrWhiteSpace(name))
            {
                // У инфобазы всегда есть имя; пустое — аномалия, fallback на UUID.
                name = id.ToString("D");
            }

            var descr = rec.GetValueOrDefault("descr");
            result.Add(new ClusterInfobase(
                id,
                name,
                string.IsNullOrWhiteSpace(descr) ? null : descr));
        }

        return result;
    }

    // --- Internals ---

    private async Task<string?> ResolveClusterUuidAsync(string exePath, CancellationToken ct)
    {
        // Кэш-хит → 0 спавнов. Single-node: один кластер, UUID стабилен (MLC-041).
        var key = BuildClusterKey(exePath);
        if (_uuidCache.TryGet(key, out var cached))
        {
            return cached;
        }

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

        if (records[0].TryGetValue("cluster", out var uuid) && !string.IsNullOrWhiteSpace(uuid))
        {
            // Кэшируем только успешный резолв — неуспех/null не кэшируется.
            _uuidCache.Store(key, uuid);
            return uuid;
        }

        return null;
    }

    // Ключ кэша UUID: ExePath + Endpoint из TTL-снапшота настроек. Пересобирается на
    // каждом вызове, поэтому смена endpoint/exePath (после Invalidate снапшота) даёт
    // промах кэша и перерезолв — отдельный хук на смену настроек не нужен.
    private ClusterUuidKey BuildClusterKey(string exePath)
        => new(exePath, _settings.GetString(SettingKey.OneCRasEndpoint) ?? string.Empty);

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

    private static bool TryParseSession(
        IReadOnlyDictionary<string, string> rec,
        HashSet<string> licenseConsumingAppIds,
        out ClusterSession session)
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
            ConsumesLicense: licenseConsumingAppIds.Contains(appId),
            StartedAtUtc: ParseUtc(rec.GetValueOrDefault("started-at")));

        return true;
    }

    private static DateTime ParseUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DateTime.UtcNow;
        }
        // rac.exe отдаёт started-at в ЛОКАЛЬНОМ времени сервера (без offset).
        // Backend спавнит rac.exe на том же хосте (ADR-3.3) → TimeZoneInfo.Local
        // корректно конвертирует в UTC. Раньше стоял AssumeUniversal — строка без
        // offset трактовалась как UTC, и на сервере UTC+3 длительность сеанса
        // занижалась на 3 ч (новые сеансы уходили в минус). Если в строке всё же
        // есть offset (на будущее) — AssumeLocal его не переопределяет.
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal,
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
