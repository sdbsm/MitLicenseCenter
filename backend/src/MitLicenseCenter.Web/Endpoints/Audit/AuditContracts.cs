using MitLicenseCenter.Domain.Audit;

namespace MitLicenseCenter.Web.Endpoints;

public sealed record AuditEntryResponse(
    Guid Id,
    DateTime Timestamp,
    AuditActionType ActionType,
    AuditReason? Reason,
    string Initiator,
    string Description,
    Guid? TenantId);

public sealed record AuditPagedResponse(
    IReadOnlyList<AuditEntryResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record AuditRetentionResponse(int RetentionDays);
