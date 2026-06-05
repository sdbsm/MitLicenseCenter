using MitLicenseCenter.Infrastructure.Discovery;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Резолвер пути к webinst.exe по версии платформы (MLC-045). webinst живёт в той же
// версионной папке, что и rac.exe: …\1cv8\<версия>\bin\webinst.exe. Перебираем те же
// корни установки (OneCInstallRoots), что и скан версий платформы — отдельная
// настройка пути не нужна (путь однозначно выводится из PlatformVersion).
internal static class WebinstExeResolver
{
    public static string? TryResolve(string platformVersion) =>
        Resolve(platformVersion, OneCInstallRoots.Get());

    // Ядро вынесено для unit-теста: принимает корни "...\1cv8".
    internal static string? Resolve(string platformVersion, IEnumerable<string> roots)
    {
        if (string.IsNullOrWhiteSpace(platformVersion))
            return null;

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;
            var candidate = Path.Combine(root, platformVersion, "bin", "webinst.exe");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }
}
