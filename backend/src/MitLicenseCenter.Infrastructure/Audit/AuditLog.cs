using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Infrastructure.Audit;

// Минимальная Stage-1 версия. В Stage 2 ActionType станет enum, добавятся TenantId FK + Reason enum.
public sealed class AuditLog : IEntity
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public required string ActionType { get; init; }
    public required string Initiator { get; init; }
    public required string Description { get; init; }
    public Guid? TenantId { get; init; }
}
