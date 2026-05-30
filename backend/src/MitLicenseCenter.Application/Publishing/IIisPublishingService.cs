using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Application.Publishing;

public interface IIisPublishingService
{
    Task<PublicationActualState> ReadActualStateAsync(Publication publication, CancellationToken ct);
    Task ApplyDesiredStateAsync(Publication publication, CancellationToken ct);

    // Discovery: список IIS-сайтов. Используется формой публикации вместо ручного
    // ввода SiteName. Может бросить исключение (нет доступа к Metabase / не Windows) —
    // вызывающий эндпоинт ловит и помечает результат как недоступный.
    Task<IReadOnlyList<IisSiteInfo>> ListSitesAsync(CancellationToken ct);
}

public sealed record IisSiteInfo(string SiteName);

public sealed record PublicationActualState(
    bool SiteExists,
    bool VirtualPathExists,
    string? PlatformVersion,
    bool EnableOData,
    bool EnableHttpServices,
    string? VrdContent,
    string? Error);
