using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Заглушка до PR 3.5, где будет реализован OneCIisPublishingService через
// Microsoft.Web.Administration + XDocument.
internal sealed class StubIisPublishingService : IIisPublishingService
{
    private static readonly PublicationActualState NotImplementedState = new(
        SiteExists: false,
        VirtualPathExists: false,
        PlatformVersion: null,
        EnableOData: false,
        EnableHttpServices: false,
        VrdContent: null,
        Error: "not implemented");

    public Task<PublicationActualState> ReadActualStateAsync(Publication publication, CancellationToken ct)
        => Task.FromResult(NotImplementedState);

    public Task ApplyDesiredStateAsync(Publication publication, CancellationToken ct)
        => Task.CompletedTask;
}
