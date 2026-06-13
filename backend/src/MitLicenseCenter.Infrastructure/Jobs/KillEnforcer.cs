using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Diagnostics;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Jobs;

internal sealed partial class KillEnforcer : IKillEnforcer
{
    private const int MaxKillsPerCycle = 20;

    // Сеансы моложе grace-порога не завершаются: в 1С `user-name` проставляется
    // только после аутентификации, а newest-first целится ровно в свежий сеанс —
    // без grace мы systematically попадаем в окно «подключился, лицензию занял,
    // но ещё не вошёл» и пишем в аудит пустого пользователя. Пауза даёт 1С
    // проставить имя; через ~hot-тик сеанс дорастает и убивается уже с именем.
    // Порог настраивается (Enforcement.KillGraceSeconds); дефолт — на случай
    // несидированной/пустой настройки.
    private const int DefaultKillGraceSeconds = 15;

    private readonly IClusterClient _cluster;
    private readonly IAuditLogger _audit;
    private readonly AppDbContext _db;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ReconciliationMetrics _metrics;
    private readonly ILogger<KillEnforcer> _logger;

    public KillEnforcer(
        IClusterClient cluster,
        IAuditLogger audit,
        AppDbContext db,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ReconciliationMetrics metrics,
        ILogger<KillEnforcer> logger)
    {
        _cluster = cluster;
        _audit = audit;
        _db = db;
        _settings = settings;
        _clock = clock;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task EnforceAsync(
        SnapshotPayload snapshot,
        IReadOnlyList<ClusterSession>? freshSessions,
        CancellationToken ct)
    {
        var tenantLimits = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .ToDictionaryAsync(t => t.Id, t => t.MaxConcurrentLicenses, ct)
            .ConfigureAwait(false);

        var consumptionByTenant = LicenseConsumption.CountByTenant(snapshot.Items);

        var overLimitTenants = LicenseConsumption.FindOverLimit(consumptionByTenant, tenantLimits);

        if (overLimitTenants.Count == 0)
            return;

        // One re-fetch for the entire enforcement cycle. Hot-путь передаёт уже полученный
        // тиком свежий список (MLC-044) — повторного спавна rac.exe нет; cold передаёт
        // null и делает свой fetch. В обоих случаях вызывающий держит IEnforcementGate,
        // поэтому список отражает kills параллельного пути (идемпотентность, anti-over-kill).
        var freshList = freshSessions ?? await _cluster.ListActiveSessionsAsync(ct).ConfigureAwait(false);
        var freshBySessionId = new Dictionary<Guid, ClusterSession>(freshList.Count);
        foreach (var s in freshList)
            freshBySessionId.TryAdd(s.SessionId, s);

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var killGracePeriod = TimeSpan.FromSeconds(
            _settings.GetInt(SettingKey.EnforcementKillGraceSeconds) ?? DefaultKillGraceSeconds);
        var totalKills = 0;

        foreach (var (tenantId, consumed, limit) in overLimitTenants)
        {
            if (totalKills >= MaxKillsPerCycle)
                break;

            var currentConsumed = consumed;

            // Candidates: sessions for this tenant that consume a license, sorted newest-first.
            var candidates = LicenseConsumption.KillCandidates(snapshot.Items, tenantId);

            foreach (var candidate in candidates)
            {
                if (currentConsumed <= limit)
                    break;
                if (totalKills >= MaxKillsPerCycle)
                    break;

                // Grace-период. Кандидаты отсортированы newest-first, поэтому первый
                // не доросший до порога сеанс — самый молодой среди оставшихся: всё,
                // что ниже по списку, старше и уже было бы убито раньше. Убивать тех,
                // более старых, вместо молодого новичка — значит рвать устоявшийся
                // рабочий сеанс ради того, кто перешагнул лимит; это противоречит
                // newest-first. Поэтому ждём (break), а не пропускаем (continue) —
                // на следующем тике молодой состарится и будет убит уже с именем.
                if (nowUtc - candidate.StartedAtUtc < killGracePeriod)
                    break;

                // Verify candidate against fresh data.
                if (!freshBySessionId.TryGetValue(candidate.SessionId, out var fresh))
                {
                    LogSessionGone(_logger, candidate.SessionId);
                    currentConsumed--;
                    continue;
                }

                if (fresh.ClusterInfobaseId != candidate.ClusterInfobaseId ||
                    fresh.AppId != candidate.AppId ||
                    fresh.StartedAtUtc != candidate.StartedAtUtc)
                {
                    LogSessionStale(_logger, candidate.SessionId);
                    continue;
                }

                var descriptor = new SessionDescriptor(
                    candidate.ClusterInfobaseId,
                    candidate.SessionId,
                    candidate.AppId,
                    candidate.StartedAtUtc);

                var result = await _cluster.KillSessionAsync(descriptor, ct).ConfigureAwait(false);

                if (result.Killed || result.AlreadyGone)
                {
                    // MLC-119 (BE-25) — enlist'им запись в общий контекст без своего SaveChanges;
                    // все записи цикла коммитятся одним round-trip ниже (короче держим замок).
                    _audit.Enlist(
                        AuditActionType.SessionKilled,
                        "System",
                        $"Сеанс {candidate.SessionId:N} ({candidate.AppId}, пользователь {SessionDisplay.UserNameOrFallback(candidate.UserName)}) завершён: превышен лимит {candidate.TenantName}.",
                        tenantId,
                        AuditReason.LimitExceeded);

                    currentConsumed--;
                    totalKills++;
                }
                else
                {
                    LogKillFailed(_logger, candidate.SessionId);
                }
            }
        }

        if (totalKills > 0)
        {
            // MLC-119 (BE-25) — один SaveChanges на все enlist'ленные SessionKilled-записи
            // цикла (вместо N round-trip'ов под замком). При totalKills==0 не зовётся.
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _metrics.AddKills(totalKills);
            LogKillSummary(_logger, totalKills);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Session {SessionId} no longer in fresh snapshot — skipping")]
    private static partial void LogSessionGone(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Session {SessionId} stale (descriptor mismatch) — skipping")]
    private static partial void LogSessionStale(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kill failed for session {SessionId} (Killed=false, AlreadyGone=false) — skipping to next cycle")]
    private static partial void LogKillFailed(ILogger logger, Guid sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Kill enforcer: terminated {Count} sessions this cycle")]
    private static partial void LogKillSummary(ILogger logger, int count);
}
