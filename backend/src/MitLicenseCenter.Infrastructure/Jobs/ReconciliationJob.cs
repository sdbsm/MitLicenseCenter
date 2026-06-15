using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Reporting;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Diagnostics;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;

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
    private readonly IEnforcementGate _gate;
    private readonly ISettingsSnapshot _settings;
    private readonly ILicenseUsageAccumulator _usage;
    private readonly ILicenseFactCache _licenseFactCache;
    private readonly TimeProvider _clock;
    private readonly ReconciliationMetrics _metrics;
    private readonly ILogger<ReconciliationJob> _logger;

    public ReconciliationJob(
        IClusterClient cluster,
        AppDbContext db,
        IActiveSessionSnapshotStore store,
        IHotTierRegistry hotTier,
        IKillEnforcer enforcer,
        IEnforcementGate gate,
        ISettingsSnapshot settings,
        ILicenseUsageAccumulator usage,
        ILicenseFactCache licenseFactCache,
        TimeProvider clock,
        ReconciliationMetrics metrics,
        ILogger<ReconciliationJob> logger)
    {
        _cluster = cluster;
        _db = db;
        _store = store;
        _hotTier = hotTier;
        _enforcer = enforcer;
        _gate = gate;
        _settings = settings;
        _usage = usage;
        _licenseFactCache = licenseFactCache;
        _clock = clock;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task RunColdAsync(CancellationToken ct)
    {
        // MLC-154: каданс задаёт таймер ColdTierPollingService (читает
        // Polling.ColdIntervalSeconds каждый цикл). Прежний внутренний throttle убран —
        // он был единственным потребителем интервала здесь и мешал бы будущему
        // «форс-прогону по запросу» (MLC-156).
        var now = _clock.GetUtcNow().UtcDateTime;

        var sw = Stopwatch.StartNew();

        try
        {
            var sessions = await _cluster.ListActiveSessionsAsync(ct).ConfigureAwait(false);

            // ADR-48 (MLC-166): второй вызов на холодном тире — факт потребления лицензии
            // (`session list --licenses`). null = факт недоступен ⇒ все сеансы Pending,
            // enforcement приостановлен (KillEnforcer ранний выход), но снапшот всё равно
            // строим для UI с LicenseFactAvailable=false.
            var licensedSet = await _cluster.ListLicensedSessionIdsAsync(ct).ConfigureAwait(false);
            var licenseFactAvailable = licensedSet is not null;

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

            // knownById покрывает ВСЕ сеансы цикла (а не только смапленные на тенант):
            // горячий тир сшивает по нему любой сеанс. Факт недоступен → пустой map +
            // available=false (всем Pending). available=true → каждый сеанс известен:
            // licensed = членство в licensedSet (отсутствие = NotConsuming).
            var knownById = new Dictionary<Guid, bool>(sessions.Count);
            if (licensedSet is not null)
            {
                foreach (var s in sessions)
                    knownById[s.SessionId] = licensedSet.Contains(s.SessionId);
            }

            var entries = new List<SnapshotSessionEntry>(sessions.Count);
            foreach (var s in sessions)
            {
                if (!infobaseMap.TryGetValue(s.ClusterInfobaseId, out var mapped))
                    continue;

                // ADR-48: факт недоступен → Pending (не присваиваем факт); иначе
                // Consuming/NotConsuming по членству в licensedSet.
                var status = licensedSet is null
                    ? LicenseStatus.Pending
                    : (licensedSet.Contains(s.SessionId) ? LicenseStatus.Consuming : LicenseStatus.NotConsuming);

                entries.Add(new SnapshotSessionEntry(
                    s.SessionId,
                    s.ClusterInfobaseId,
                    mapped.Id,
                    mapped.TenantName,
                    mapped.InfobaseName,
                    s.AppId,
                    s.UserName,
                    s.Host,
                    status,
                    s.StartedAtUtc));
            }

            // Обновляем кэш факта для горячего тира (пишет только холодный цикл).
            _licenseFactCache.Update(knownById, licenseFactAvailable);

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

            // MLC-048 (ADR-25): семпл потребления по всем активным тенантам цикла (идлы=0,
            // чтобы min/avg были честными). Переиспользуем consumptionByTenant/infobaseMap —
            // нового спавна rac.exe не вводим (ADR-3.3/16). distinct по тенанту: у клиента
            // может быть >1 инфобазы. Аккумулятор вернёт строки только на границе бакета.
            var usageSamples = infobaseMap.Values
                .Where(m => m.IsActive)
                .GroupBy(m => m.Id)
                .Select(g => new LicenseUsageSample(
                    g.Key,
                    consumptionByTenant.GetValueOrDefault(g.Key, 0),
                    g.First().MaxConcurrentLicenses))
                .ToList();
            var usageBuckets = _usage.RecordSample(now, usageSamples);

            sw.Stop();
            _metrics.RecordColdCycle(sw.Elapsed.TotalMilliseconds);
            var payload = new SnapshotPayload(entries, now, (int)sw.ElapsedMilliseconds, AdapterSource, licenseFactAvailable);

            // MLC-044: enforce под общим замком. freshSessions=null → enforcer делает свой
            // re-fetch ВНУТРИ замка (cold-профиль спавнов 1:1: snapshot-list + enforce-refetch).
            // Замок сериализует cold с hot-циклом → over-kill невозможен (MLC-001).
            using (await _gate.AcquireAsync(ct).ConfigureAwait(false))
            {
                await _enforcer.EnforceAsync(payload, freshSessions: null, ct).ConfigureAwait(false);
            }

            _store.Replace(payload);

            // MLC-048: персист телеметрии — вне enforcement-замка (не блокирует kill-путь).
            // Пишем только на границе бакета, когда аккумулятор вернул готовые строки.
            if (usageBuckets.Count > 0)
            {
                _db.LicenseUsageSnapshots.AddRange(usageBuckets.Select(b => new LicenseUsageSnapshot
                {
                    Id = Guid.NewGuid(),
                    TenantId = b.TenantId,
                    BucketStartUtc = b.BucketStartUtc,
                    ConsumedMin = b.ConsumedMin,
                    ConsumedMax = b.ConsumedMax,
                    ConsumedAvg = b.ConsumedAvg,
                    Limit = b.Limit,
                }));
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

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
