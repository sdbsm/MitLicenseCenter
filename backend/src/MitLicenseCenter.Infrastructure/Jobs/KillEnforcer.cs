using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Jobs;

internal sealed partial class KillEnforcer : IKillEnforcer
{
    private const int MaxKillsPerCycle = 20;

    private readonly IClusterClient _cluster;
    private readonly IAuditLogger _audit;
    private readonly AppDbContext _db;
    private readonly ILogger<KillEnforcer> _logger;

    public KillEnforcer(
        IClusterClient cluster,
        IAuditLogger audit,
        AppDbContext db,
        ILogger<KillEnforcer> logger)
    {
        _cluster = cluster;
        _audit = audit;
        _db = db;
        _logger = logger;
    }

    public async Task EnforceAsync(SnapshotPayload snapshot, CancellationToken ct)
    {
        var tenantLimits = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive)
            .ToDictionaryAsync(t => t.Id, t => t.MaxConcurrentLicenses, ct)
            .ConfigureAwait(false);

        var consumptionByTenant = snapshot.Items
            .Where(e => e.ConsumesLicense)
            .GroupBy(e => e.TenantId)
            .ToDictionary(g => g.Key, g => g.Count());

        var overLimitTenants = new List<(Guid TenantId, int Consumed, int Limit)>();
        foreach (var (tenantId, consumed) in consumptionByTenant)
        {
            if (!tenantLimits.TryGetValue(tenantId, out var limit))
                continue;
            if (limit <= 0)
                continue;
            if (consumed <= limit)
                continue;
            overLimitTenants.Add((tenantId, consumed, limit));
        }

        if (overLimitTenants.Count == 0)
            return;

        // One re-fetch call for the entire enforcement cycle.
        var freshSessions = await _cluster.ListActiveSessionsAsync(ct).ConfigureAwait(false);
        var freshBySessionId = new Dictionary<Guid, ClusterSession>(freshSessions.Count);
        foreach (var s in freshSessions)
            freshBySessionId.TryAdd(s.SessionId, s);

        var totalKills = 0;

        foreach (var (tenantId, consumed, limit) in overLimitTenants)
        {
            if (totalKills >= MaxKillsPerCycle)
                break;

            var currentConsumed = consumed;

            // Candidates: sessions for this tenant that consume a license, sorted newest-first.
            var candidates = snapshot.Items
                .Where(e => e.TenantId == tenantId && e.ConsumesLicense)
                .OrderByDescending(e => e.StartedAtUtc)
                .ToList();

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
