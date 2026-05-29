using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Jobs;

internal sealed partial class HotTierPollingService : BackgroundService
{
    // См. ReconciliationJob.AdapterSource — общее обоснование.
    private const string AdapterSource = "Ras";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IActiveSessionSnapshotStore _store;
    private readonly IHotTierRegistry _registry;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<HotTierPollingService> _logger;

    public HotTierPollingService(
        IServiceScopeFactory scopeFactory,
        IActiveSessionSnapshotStore store,
        IHotTierRegistry registry,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ILogger<HotTierPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _store = store;
        _registry = registry;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = _settings.GetInt(SettingKey.PollingHotIntervalSeconds) ?? 4;
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken).ConfigureAwait(false);

            var hotTenants = _registry.CurrentHotTenants();
            if (hotTenants.Count == 0)
                continue;

            try
            {
                var sw = Stopwatch.StartNew();

                using var scope = _scopeFactory.CreateScope();
                var cluster = scope.ServiceProvider.GetRequiredService<IClusterClient>();

                var freshSessions = await cluster.ListActiveSessionsAsync(stoppingToken).ConfigureAwait(false);
                sw.Stop();

                var currentPayload = _store.Current();

                // Build denormalization lookup from the current (cold) snapshot.
                var infobaseLookup = new Dictionary<Guid, SnapshotSessionEntry>();
                foreach (var item in currentPayload.Items)
                    infobaseLookup.TryAdd(item.ClusterInfobaseId, item);

                var hotTenantSet = hotTenants.ToHashSet();

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
                        s.ConsumesLicense,
                        s.StartedAtUtc));
                }

                // Overlay: keep cold rows for non-hot tenants, replace hot tenant rows with fresh.
                var overlaid = currentPayload.Items
                    .Where(x => !hotTenantSet.Contains(x.TenantId))
                    .Concat(freshEntries)
                    .ToList();

                var now = _clock.GetUtcNow().UtcDateTime;
                _store.Replace(new SnapshotPayload(overlaid, now, (int)sw.ElapsedMilliseconds, AdapterSource));

                LogHotOverlay(_logger, hotTenants.Count, freshEntries.Count, (int)sw.ElapsedMilliseconds);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot-tier polling service started")]
    private static partial void LogStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot-tier polling service stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hot overlay: {HotCount} hot tenant(s), {FreshCount} fresh sessions, {Elapsed}ms")]
    private static partial void LogHotOverlay(ILogger logger, int hotCount, int freshCount, int elapsed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hot-tier polling cycle failed")]
    private static partial void LogHotFailed(ILogger logger, Exception ex);
}
