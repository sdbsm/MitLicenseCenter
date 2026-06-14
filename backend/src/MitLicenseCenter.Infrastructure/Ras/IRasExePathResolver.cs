namespace MitLicenseCenter.Infrastructure.Ras;

// Резолвер пути к ras.exe выбранной версии платформы. Вынесен за интерфейс, чтобы
// ScRasServiceManager тестировался без реальной ФС (production-реализация бьёт по
// каталогам 1С через RasExePathDiscovery).
internal interface IRasExePathResolver
{
    string? ResolveForVersion(string platformVersion);
}

internal sealed class RasExePathResolver : IRasExePathResolver
{
    public string? ResolveForVersion(string platformVersion)
        => RasExePathDiscovery.ResolveForVersion(platformVersion);
}
