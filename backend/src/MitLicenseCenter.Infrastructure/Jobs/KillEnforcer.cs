using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Diagnostics;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Jobs;

internal sealed partial class KillEnforcer : IKillEnforcer
{
    private const int MaxKillsPerCycle = 20;

    private readonly IClusterClient _cluster;
    private readonly IAuditLogger _audit;
    private readonly AppDbContext _db;
    private readonly ReconciliationMetrics _metrics;
    private readonly ILogger<KillEnforcer> _logger;

    public KillEnforcer(
        IClusterClient cluster,
        IAuditLogger audit,
        AppDbContext db,
        ReconciliationMetrics metrics,
        ILogger<KillEnforcer> logger)
    {
        _cluster = cluster;
        _audit = audit;
        _db = db;
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
                    await _audit.LogAsync(
                        AuditActionType.SessionKilled,
                        "System",
                        $"Сеанс {candidate.SessionId:N} ({candidate.AppId}, пользователь {candidate.UserName}) завершён: превышен лимит {candidate.TenantName}.",
                        tenantId,
                        AuditReason.LimitExceeded,
                        ct).ConfigureAwait(false);

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
