using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Infrastructure.Publishing.Testing;

// Заглушка для unit-тестов (DriftCheckTransitionAuditTests,
// PublicationsReconcileTests), которым не нужен реальный ServerManager.
// В production-DI больше не регистрируется — с PR 3.5 подключён реальный
// адаптер OneCIisPublishingService.
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

    // Нет реального IIS — пустой список сайтов.
    public Task<IReadOnlyList<IisSiteInfo>> ListSitesAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IisSiteInfo>>(Array.Empty<IisSiteInfo>());
}
