namespace MitLicenseCenter.Infrastructure.Publishing;

// Pure static helper (PR 4.1): вычисляет полный путь к default.vrd.
// Следует паттерну VrdPatcher/PublicationDriftDetector — без зависимостей на IIS,
// unit-testable напрямую из MitLicenseCenter.Tests.Unit/Publishing/.
//
// Semantics: PhysicalPathOverride = физическая папка IIS-приложения (то, что
// оператор видит в IIS Manager → Physical path). Метод appends \default.vrd.
// Конвенция fallback: {IIS.DefaultVrdRoot}/{siteName}/{trimmedVirtualPath}/default.vrd.
internal static class VrdPathResolver
{
    internal static string Resolve(
        string? physicalPathOverride,
        string defaultVrdRoot,
        string siteName,
        string virtualPath)
    {
        if (!string.IsNullOrWhiteSpace(physicalPathOverride))
        {
            // TrimEnd гарантирует clean path независимо от того, ввёл ли оператор
            // trailing slash: «C:\path\» → «C:\path\default.vrd» (не «C:\path\\default.vrd»).
            return Path.Combine(physicalPathOverride.TrimEnd('\\', '/'), "default.vrd");
        }

        var trimmed = virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(defaultVrdRoot, siteName, trimmed, "default.vrd");
    }
}
