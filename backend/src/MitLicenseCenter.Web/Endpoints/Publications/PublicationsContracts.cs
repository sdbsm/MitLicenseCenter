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
    DateTime? UpdatedAt,
    PublicationDriftStatus LastDriftStatus,
    DateTime? LastDriftCheckAt,
    string? LastDriftDetails,
    string? PhysicalPathOverride);

public sealed record CheckDriftAcceptedResponse(string CorrelationId, Guid PublicationId);

public sealed record DriftStatusResponse(
    PublicationDriftStatus Status,
    DateTime? CheckedAt,
    string? Details);

public sealed record CreatePublicationRequest(
    [property: Required, StringLength(InfobaseValidationRules.SiteNameMaxLength, MinimumLength = 1)] string SiteName,
    [property: Required, StringLength(InfobaseValidationRules.VirtualPathMaxLength, MinimumLength = 1)] string VirtualPath,
    [property: Required, StringLength(InfobaseValidationRules.PlatformVersionMaxLength, MinimumLength = 1)] string PlatformVersion,
    bool EnableOData,
    bool EnableHttpServices,
    [property: StringLength(InfobaseValidationRules.VrdCustomXmlMaxLength)] string? VrdCustomXml,
    [property: StringLength(InfobaseValidationRules.PhysicalPathMaxLength)] string? PhysicalPathOverride);

public sealed record UpdatePublicationRequest(
    [property: Required, StringLength(InfobaseValidationRules.SiteNameMaxLength, MinimumLength = 1)] string SiteName,
    [property: Required, StringLength(InfobaseValidationRules.VirtualPathMaxLength, MinimumLength = 1)] string VirtualPath,
    [property: Required, StringLength(InfobaseValidationRules.PlatformVersionMaxLength, MinimumLength = 1)] string PlatformVersion,
    bool EnableOData,
    bool EnableHttpServices,
    [property: StringLength(InfobaseValidationRules.VrdCustomXmlMaxLength)] string? VrdCustomXml,
    [property: StringLength(InfobaseValidationRules.PhysicalPathMaxLength)] string? PhysicalPathOverride);

internal static class PublicationMappings
{
    public static PublicationResponse ToResponse(this Publication x) =>
        new(
            x.Id,
            x.InfobaseId,
            x.SiteName,
            x.VirtualPath,
            x.PlatformVersion,
            x.EnableOData,
            x.EnableHttpServices,
            x.VrdCustomXml,
            x.CreatedAt,
            x.UpdatedAt,
            x.LastDriftStatus,
            x.LastDriftCheckAt,
            x.LastDriftDetails,
            x.PhysicalPathOverride);
}
