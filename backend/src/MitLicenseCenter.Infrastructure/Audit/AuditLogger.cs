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

    public async Task LogAsync(
        AuditActionType action,
        string initiator,
        string description,
        Guid? tenantId = null,
        AuditReason? reason = null,
        CancellationToken ct = default)
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

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
