using System.ComponentModel.DataAnnotations;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Web.Endpoints;

public sealed record PublicationResponse(
    Guid Id,
    Guid InfobaseId,
    string SiteName,
    string VirtualPath,
    string PlatformVersion,
    bool EnableOData,
    bool EnableHttpServices,
    string? VrdCustomXml,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreatePublicationRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string SiteName,
    [property: Required, StringLength(200, MinimumLength = 1)] string VirtualPath,
    [property: Required, StringLength(50, MinimumLength = 1)] string PlatformVersion,
    bool EnableOData,
    bool EnableHttpServices,
    [property: StringLength(8000)] string? VrdCustomXml);

public sealed record UpdatePublicationRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string SiteName,
    [property: Required, StringLength(200, MinimumLength = 1)] string VirtualPath,
    [property: Required, StringLength(50, MinimumLength = 1)] string PlatformVersion,
    bool EnableOData,
    bool EnableHttpServices,
    [property: StringLength(8000)] string? VrdCustomXml);

internal static class PublicationMappings
{
    public static PublicationResponse ToResponse(this Publication x) =>
        new(x.Id, x.InfobaseId, x.SiteName, x.VirtualPath, x.PlatformVersion, x.EnableOData, x.EnableHttpServices, x.VrdCustomXml, x.CreatedAt, x.UpdatedAt);
}
