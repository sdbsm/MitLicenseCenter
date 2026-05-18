using MitLicenseCenter.Domain.Audit;

namespace MitLicenseCenter.Application.Auditing;

public interface IAuditLogger
{
    Task LogAsync(
        AuditActionType action,
        string initiator,
        string description,
        Guid? tenantId = null,
        AuditReason? reason = null,
        CancellationToken ct = default);
}
