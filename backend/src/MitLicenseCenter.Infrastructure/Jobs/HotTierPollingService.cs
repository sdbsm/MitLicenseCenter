using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Diagnostics;

namespace MitLicenseCenter.Infrastructure.Jobs;

internal sealed partial class HotTierPollingService : BackgroundService
{
    // См. ReconciliationJob.AdapterSource — общее обоснование.
    private const string AdapterSource = "Ras";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IActiveSessionSnapshotStore _store;
    private readonly IHotTierRegistry _registry;
    private readonly IEnforcementGate _gate;
    private readonly ISettingsSnapshot _settings;
    private readonly ILicenseFactCache _licenseFactCache;
    private readonly TimeProvider _clock;
    private readonly ReconciliationMetrics _metrics;
    private readonly ILogger<HotTierPollingService> _logger;

    public HotTierPollingService(
        IServiceScopeFactory scopeFactory,
        IActiveSessionSnapshotStore store,
        IHotTierRegistry registry,
        IEnforcementGate gate,
        ISettingsSnapshot settings,
        ILicenseFactCache licenseFactCache,
        TimeProvider clock,
        ReconciliationMetrics metrics,
        ILogger<HotTierPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _store = store;
        _registry = registry;
        _gate = gate;
        _settings = settings;
        _licenseFactCache = licenseFactCache;
        _clock = clock;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = _settings.GetInt(SettingKey.PollingHotIntervalSeconds) ?? 4;
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken).ConfigureAwait(false);

            if (_registry.CurrentHotTenants().Count == 0)
                continue;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cluster = scope.ServiceProvider.GetRequiredService<IClusterClient>();
                var enforcer = scope.ServiceProvider.GetRequiredService<IKillEnforcer>();

                await RunCycleOnceAsync(cluster, enforcer, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogHotFailed(_logger, ex);
            }
        }

        LogStopped(_logger);
    }

    // Один тик hot-цикла: fetch свежих сессий → overlay-снимок для UI → enforce по
    // hot-тенантам ТЕМ ЖЕ списком. Internal — детерминированный seam для теста
    // over-kill (без драйва BackgroundService). MLC-044.
    internal async Task RunCycleOnceAsync(
        IClusterClient cluster,
        IKillEnforcer enforcer,
        CancellationToken ct)
    {
        var hotTenants = _registry.CurrentHotTenants();
        if (hotTenants.Count == 0)
            return;

        // Замок берётся ДО fetch'а: единственный список тика обслуживает и overlay, и
        // fresh-проверку enforcement, и при этом cold не может вклиниться между fetch'ем
        // и kills (иначе список устарел бы → over-kill). MLC-044.
        using var _ = await _gate.AcquireAsync(ct).ConfigureAwait(false);

        var sw = Stopwatch.StartNew();
        var freshSessions = await cluster.ListActiveSessionsAsync(ct).ConfigureAwait(false);
        sw.Stop();
        _metrics.RecordHotCycle(sw.Elapsed.TotalMilliseconds);

        var currentPayload = _store.Current();

        // Build denormalization lookup from the current (cold) snapshot.
        var infobaseLookup = new Dictionary<Guid, SnapshotSessionEntry>();
        foreach (var item in currentPayload.Items)
            infobaseLookup.TryAdd(item.ClusterInfobaseId, item);

        var hotTenantSet = hotTenants.ToHashSet();

        // ADR-48 (MLC-166): горячий тир НЕ спавнит `--licenses` (один вызов rac на тик).
        // Классификацию берём из последнего холодного факта: известен → Consuming/
        // NotConsuming, неизвестен (свежий сеанс ещё не в факт-снимке) → Pending.
        var licenseFact = _licenseFactCache.Current();

        // Denormalize fresh sessions for hot tenants only.
        var freshEntries = new List<SnapshotSessionEntry>();
        foreach (var s in freshSessions)
        {
            if (!infobaseLookup.TryGetValue(s.ClusterInfobaseId, out var mapped))
                continue;

            if (!hotTenantSet.Contains(mapped.TenantId))
                continue;

            freshEntries.Add(new SnapshotSessionEntry(
                s.SessionId,
                s.ClusterInfobaseId,
                mapped.TenantId,
                mapped.TenantName,
                mapped.InfobaseName,
                s.AppId,
                s.UserName,
                s.Host,
                licenseFact.Classify(s.SessionId),
                s.StartedAtUtc));
        }

        // Overlay: keep cold rows for non-hot tenants, replace hot tenant rows with fresh.
        var overlaid = currentPayload.Items
            .Where(x => !hotTenantSet.Contains(x.TenantId))
            .Concat(freshEntries)
            .ToList();

        var now = _clock.GetUtcNow().UtcDateTime;
        _store.Replace(new SnapshotPayload(
            overlaid, now, (int)sw.ElapsedMilliseconds, AdapterSource, licenseFact.Available));

        // MLC-044: enforce строго по hot-тенантам (базис = freshEntries, чисто свежие
        // данные). over-limit (Consumed>Limit, >100%) ⊆ hot (≥90%), поэтому non-hot строки
        // в базис не входят и kills не дают. freshSessions переиспользуется как
        // fresh-проверка — второго спавна rac.exe нет (ADR-3.3).
        var hotPayload = new SnapshotPayload(
            freshEntries, now, (int)sw.ElapsedMilliseconds, AdapterSource, licenseFact.Available);
        await enforcer.EnforceAsync(hotPayload, freshSessions, ct).ConfigureAwait(false);

        LogHotOverlay(_logger, hotTenants.Count, freshEntries.Count, (int)sw.ElapsedMilliseconds);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot-tier polling service started")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot-tier polling service stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hot overlay: {HotCount} hot tenant(s), {FreshCount} fresh sessions, {Elapsed}ms")]
    private static partial void LogHotOverlay(ILogger logger, int hotCount, int freshCount, int elapsed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hot-tier polling cycle failed")]
    private static partial void LogHotFailed(ILogger logger, Exception ex);
}
