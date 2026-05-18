using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Infrastructure.Audit;

public sealed class AuditLog : IEntity
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public AuditActionType ActionType { get; init; }
    public AuditReason? Reason { get; init; }
    public required string Initiator { get; init; }
    public required string Description { get; init; }
    public Guid? TenantId { get; init; }
}
