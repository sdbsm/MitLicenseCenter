using System.ComponentModel.DataAnnotations;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Web.Endpoints;

public sealed record PublicationResponse(
    Guid Id,
    Guid InfobaseId,
    string SiteName,
    string VirtualPath,
    string PlatformVersion,
    PublicationSource Source,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    PublicationPublishStatus LastCheckStatus,
    DateTime? LastCheckAt,
    string? LastCheckDetails,
    string? PhysicalPathOverride);

// Ответ проверки/смены платформы — текущий read-only статус публикации.
public sealed record PublicationStatusResponse(
    PublicationPublishStatus Status,
    DateTime? CheckedAt,
    string? Details);

// Запрос публикации через webinst. Confirm=true снимает гейт на перезапись
// публикации, происхождение которой не Webinst (перезатрёт ручную конфигурацию).
public sealed record PublishPublicationRequest(bool Confirm);

// Запрос смены платформы — правит путь к wsisapi.dll в web.config под новую версию.
public sealed record ChangePlatformRequest(
    [property: Required, StringLength(InfobaseValidationRules.PlatformVersionMaxLength, MinimumLength = 1)] string PlatformVersion);

public sealed record CreatePublicationRequest(
    [property: Required, StringLength(InfobaseValidationRules.SiteNameMaxLength, MinimumLength = 1)] string SiteName,
    [property: Required, StringLength(InfobaseValidationRules.VirtualPathMaxLength, MinimumLength = 1)] string VirtualPath,
    [property: Required, StringLength(InfobaseValidationRules.PlatformVersionMaxLength, MinimumLength = 1)] string PlatformVersion,
    [property: StringLength(InfobaseValidationRules.PhysicalPathMaxLength)] string? PhysicalPathOverride);

public sealed record UpdatePublicationRequest(
    [property: Required, StringLength(InfobaseValidationRules.SiteNameMaxLength, MinimumLength = 1)] string SiteName,
    [property: Required, StringLength(InfobaseValidationRules.VirtualPathMaxLength, MinimumLength = 1)] string VirtualPath,
    [property: Required, StringLength(InfobaseValidationRules.PlatformVersionMaxLength, MinimumLength = 1)] string PlatformVersion,
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
            x.Source,
            x.CreatedAt,
            x.UpdatedAt,
            x.LastCheckStatus,
            x.LastCheckAt,
            x.LastCheckDetails,
            x.PhysicalPathOverride);
}
