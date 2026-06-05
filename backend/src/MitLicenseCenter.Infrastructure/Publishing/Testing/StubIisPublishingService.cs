using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Infrastructure.Publishing.Testing;

// Заглушка для unit-тестов (статус-job, change-platform endpoint), которым не нужен
// реальный ServerManager. В production-DI не регистрируется — реальный
// OneCIisPublishingService требует Windows. Настраивается через публичные поля.
internal sealed class StubIisPublishingService : IIisPublishingService
{
    // Состояние, которое вернёт ReadActualStateAsync. По умолчанию — «опубликована».
    public PublicationActualState ActualState { get; set; } = new(
        SiteExists: true,
        VirtualPathExists: true,
        WebConfigExists: true,
        PlatformVersion: "8.3.23.1865",
        Error: null);

    // Если задано — ChangePlatformAsync бросает это исключение (тест 409-путей).
    public Exception? ChangePlatformThrows { get; set; }

    public int ChangePlatformCalls { get; private set; }
    public string? LastChangePlatformVersion { get; private set; }

    public Task<PublicationActualState> ReadActualStateAsync(Publication publication, CancellationToken ct)
        => Task.FromResult(ActualState);

    public Task ChangePlatformAsync(Publication publication, string newVersion, CancellationToken ct)
    {
        ChangePlatformCalls++;
        LastChangePlatformVersion = newVersion;
        if (ChangePlatformThrows is not null)
            throw ChangePlatformThrows;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IisSiteInfo>> ListSitesAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<IisSiteInfo>>(Array.Empty<IisSiteInfo>());
}
