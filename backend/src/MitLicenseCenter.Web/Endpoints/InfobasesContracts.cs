using System.ComponentModel.DataAnnotations;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Web.Endpoints;

public sealed record InfobaseResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    Guid ClusterInfobaseId,
    string DatabaseServer,
    string DatabaseName,
    InfobaseStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record InfobaseListItemResponse(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string Name,
    Guid ClusterInfobaseId,
    string DatabaseServer,
    string DatabaseName,
    InfobaseStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    PublicationResponse Publication);

public sealed record InfobaseListResponse(
    IReadOnlyList<InfobaseListItemResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record InfobaseDetailResponse(
    InfobaseResponse Infobase,
    PublicationResponse Publication);

public sealed record CreateInfobaseRequest(
    [property: Required] Guid TenantId,
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: Required] Guid ClusterInfobaseId,
    [property: Required, StringLength(200, MinimumLength = 1)] string DatabaseServer,
    [property: Required, StringLength(200, MinimumLength = 1)] string DatabaseName,
    [property: Required] InfobaseStatus Status,
    [property: Required] CreatePublicationRequest Publication);

public sealed record UpdateInfobaseRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: Required] Guid ClusterInfobaseId,
    [property: Required, StringLength(200, MinimumLength = 1)] string DatabaseServer,
    [property: Required, StringLength(200, MinimumLength = 1)] string DatabaseName,
    [property: Required] InfobaseStatus Status,
    [property: Required] UpdatePublicationRequest Publication);

internal static class InfobaseMappings
{
    public static InfobaseResponse ToResponse(this Infobase x) =>
        new(x.Id, x.TenantId, x.Name, x.ClusterInfobaseId, x.DatabaseServer, x.DatabaseName, x.Status, x.CreatedAt, x.UpdatedAt);

    public static InfobaseDetailResponse ToDetailResponse(this Infobase infobase, Publication publication) =>
        new(infobase.ToResponse(), publication.ToResponse());
}
