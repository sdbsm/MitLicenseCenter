using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Application.Publishing;

public interface IIisPublishingService
{
    Task<PublicationActualState> ReadActualStateAsync(Publication publication, CancellationToken ct);
    Task ApplyDesiredStateAsync(Publication publication, CancellationToken ct);
}

public sealed record PublicationActualState(
    bool SiteExists,
    bool VirtualPathExists,
    string? PlatformVersion,
    bool EnableOData,
    bool EnableHttpServices,
    string? VrdContent,
    string? Error);
