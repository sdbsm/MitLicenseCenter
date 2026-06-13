using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Audit;

internal sealed class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public AuditLogger(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    // MLC-119 (BE-01/BE-25) — добавляет аудит-запись в общий tracked-контекст БЕЗ SaveChanges.
    // Запись закоммитится тем же SaveChangesAsync, что и операция вызывающего (атомарность),
    // либо одним батч-SaveChanges на несколько enlist-записей (KillEnforcer).
    public void Enlist(
        AuditActionType action,
        string initiator,
        string description,
        Guid? tenantId = null,
        AuditReason? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initiator);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = _clock.GetUtcNow().UtcDateTime,
            ActionType = action,
            Initiator = initiator,
            Description = description,
            TenantId = tenantId,
            Reason = reason,
        });
    }

    // LogAsync = Enlist + собственный SaveChanges (поведение 1:1 прежнее) — DRY поверх Enlist.
    public async Task LogAsync(
        AuditActionType action,
        string initiator,
        string description,
        Guid? tenantId = null,
        AuditReason? reason = null,
        CancellationToken ct = default)
    {
        Enlist(action, initiator, description, tenantId, reason);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
