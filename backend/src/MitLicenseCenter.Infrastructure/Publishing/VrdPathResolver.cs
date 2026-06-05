namespace MitLicenseCenter.Infrastructure.Publishing;

// Pure static helper (PR 4.1 + MLC-045): вычисляет физическую папку публикации и
// пути к её файлам (default.vrd, web.config). Без зависимостей на IIS,
// unit-testable напрямую из MitLicenseCenter.Tests.Unit/Publishing/.
//
// Semantics: PhysicalPathOverride = физическая папка IIS-приложения (то, что
// оператор видит в IIS Manager → Physical path). Конвенция fallback:
// {IIS.DefaultVrdRoot}/{siteName}/{trimmedVirtualPath}.
internal static class VrdPathResolver
{
    // Физическая папка публикации (без имени файла).
    internal static string ResolveDirectory(
        string? physicalPathOverride,
        string defaultVrdRoot,
        string siteName,
        string virtualPath)
    {
        if (!string.IsNullOrWhiteSpace(physicalPathOverride))
        {
            // TrimEnd гарантирует clean path независимо от того, ввёл ли оператор
            // trailing slash: «C:\path\» → «C:\path» (не «C:\path\»).
            return physicalPathOverride.TrimEnd('\\', '/');
        }

        var trimmed = virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(defaultVrdRoot, siteName, trimmed);
    }

    internal static string Resolve(
        string? physicalPathOverride,
        string defaultVrdRoot,
        string siteName,
        string virtualPath) =>
        Path.Combine(
            ResolveDirectory(physicalPathOverride, defaultVrdRoot, siteName, virtualPath),
            "default.vrd");

    internal static string ResolveWebConfig(
        string? physicalPathOverride,
        string defaultVrdRoot,
        string siteName,
        string virtualPath) =>
        Path.Combine(
            ResolveDirectory(physicalPathOverride, defaultVrdRoot, siteName, virtualPath),
            "web.config");
}
