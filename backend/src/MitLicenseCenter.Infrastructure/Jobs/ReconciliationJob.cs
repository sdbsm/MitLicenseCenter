using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Diagnostics;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Jobs;

internal sealed partial class ReconciliationJob : IReconciliationJob
{
    // После Stage 5 PR 5.1 (ADR-16) единственный адаптер — RAS via rac.exe.
    // SnapshotPayload.Source хардкодим "Ras" — поле зарезервировано на случай
    // будущей детализации (RasCli vs RasSocket для Strategy B).
    private const string AdapterSource = "Ras";

    private readonly IClusterClient _cluster;
    private readonly AppDbContext _db;
    private readonly IActiveSessionSnapshotStore _store;
    private readonly IHotTierRegistry _hotTier;
    private readonly IKillEnforcer _enforcer;
    private readonly ISettingsSnapshot _settings;
    private readonly ColdThrottleState _throttle;
    private readonly TimeProvider _clock;
    private readonly ReconciliationMetrics _metrics;
    private readonly ILogger<ReconciliationJob> _logger;

    public ReconciliationJob(
        IClusterClient cluster,
        AppDbContext db,
        IActiveSessionSnapshotStore store,
        IHotTierRegistry hotTier,
        IKillEnforcer enforcer,
        ISettingsSnapshot settings,
        ColdThrottleState throttle,
        TimeProvider clock,
        ReconciliationMetrics metrics,
        ILogger<ReconciliationJob> logger)
    {
        _cluster = cluster;
        _db = db;
        _store = store;
        _hotTier = hotTier;
        _enforcer = enforcer;
        _settings = settings;
        _throttle = throttle;
        _clock = clock;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task RunColdAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var coldIntervalSeconds = _settings.GetInt(SettingKey.PollingColdIntervalSeconds) ?? 25;

        if ((now - _throttle.LastRunAttemptUtc).TotalSeconds < coldIntervalSeconds)
            return;

        _throttle.MarkRun(now);

        var sw = Stopwatch.StartNew();

        try
        {
            var sessions = await _cluster.ListActiveSessionsAsync(ct).ConfigureAwait(false);

            var infobaseMap = await _db.Infobases
                .AsNoTracking()
                .Join(
                    _db.Tenants.AsNoTracking(),
                    i => i.TenantId,
                    t => t.Id,
                    (i, t) => new
                    {
                        i.ClusterInfobaseId,
                        t.Id,
                        TenantName = t.Name,
                        InfobaseName = i.Name,
                        t.MaxConcurrentLicenses,
                        t.IsActive,
                    })
                .ToDictionaryAsync(x => x.ClusterInfobaseId, ct)
                .ConfigureAwait(false);

            var entries = new List<SnapshotSessionEntry>(sessions.Count);
            foreach (var s in sessions)
            {
                if (!infobaseMap.TryGetValue(s.ClusterInfobaseId, out var mapped))
                    continue;

                entries.Add(new SnapshotSessionEntry(
                    s.SessionId,
                    s.ClusterInfobaseId,
                    mapped.Id,
                    mapped.TenantName,
                    mapped.InfobaseName,
                    s.AppId,
                    s.UserName,
                    s.Host,
                    s.ConsumesLicense,
                    s.StartedAtUtc));
            }

            var threshold = _settings.GetInt(SettingKey.PollingHotThresholdPercent) ?? 90;
            var promotedThisCycle = new HashSet<Guid>();

            var consumptionByTenant = LicenseConsumption.CountByTenant(entries);

            foreach (var (clusterInfobaseId, mapped) in infobaseMap)
            {
                if (!mapped.IsActive || mapped.MaxConcurrentLicenses <= 0)
                    continue;

                var consumed = consumptionByTenant.GetValueOrDefault(mapped.Id, 0);
                var percent = (double)consumed / mapped.MaxConcurrentLicenses * 100;

                if (percent >= threshold)
                {
                    _hotTier.Promote(mapped.Id);
                    promotedThisCycle.Add(mapped.Id);
                }
            }

            foreach (var hotTenantId in _hotTier.CurrentHotTenants())
            {
                if (!promotedThisCycle.Contains(hotTenantId))
                    _hotTier.Demote(hotTenantId);
            }

            sw.Stop();
            _metrics.RecordColdCycle(sw.Elapsed.TotalMilliseconds);
            var payload = new SnapshotPayload(entries, now, (int)sw.ElapsedMilliseconds, AdapterSource);

            await _enforcer.EnforceAsync(payload, ct).ConfigureAwait(false);

            _store.Replace(payload);

            LogColdSnapshot(_logger, entries.Count, (int)sw.ElapsedMilliseconds, AdapterSource);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogColdFailed(_logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cold snapshot: {Count} sessions, {Elapsed}ms, source={Source}")]
    private static partial void LogColdSnapshot(ILogger logger, int count, int elapsed, string source);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cold reconciliation cycle failed")]
    private static partial void LogColdFailed(ILogger logger, Exception ex);
}
