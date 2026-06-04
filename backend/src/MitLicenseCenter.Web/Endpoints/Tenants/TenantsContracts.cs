using System.ComponentModel.DataAnnotations;
using MitLicenseCenter.Domain.Tenants;

namespace MitLicenseCenter.Web.Endpoints;

public sealed record TenantResponse(
    Guid Id,
    string Name,
    int MaxConcurrentLicenses,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int InfobaseCount = 0);

public sealed record TenantListResponse(
    IReadOnlyList<TenantResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record CreateTenantRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: Range(0, 100_000)] int MaxConcurrentLicenses,
    bool IsActive);

public sealed record UpdateTenantRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: Range(0, 100_000)] int MaxConcurrentLicenses,
    bool IsActive);

internal static class TenantMappings
{
    public static TenantResponse ToResponse(this Tenant t) =>
        new(t.Id, t.Name, t.MaxConcurrentLicenses, t.IsActive, t.CreatedAt, t.UpdatedAt);
}
