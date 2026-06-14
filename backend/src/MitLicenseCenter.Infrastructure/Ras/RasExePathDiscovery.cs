using MitLicenseCenter.Infrastructure.Discovery;

namespace MitLicenseCenter.Infrastructure.Ras;

// Скан стандартных каталогов установки 1С на наличие ras.exe:
// {ProgramFiles}\1cv8\{version}\bin\ras.exe — ras.exe лежит рядом с rac.exe
// (та же версионная папка bin). Чистая работа с ФС, без процессов. Переиспользует
// общий источник корней OneCInstallRoots (как RacPathDiscovery / PlatformVersionDiscovery).
internal static class RasExePathDiscovery
{
    public static string ExeFileName => "ras.exe";

    // Путь к ras.exe конкретной версии платформы (для register/update под выбранную
    // OneC.DefaultPlatformVersion). null, если папки версии или файла нет.
    public static string? ResolveForVersion(string platformVersion)
        => ResolveForVersion(OneCInstallRoots.Get(), platformVersion);

    // Ядро вынесено для unit-теста: принимает каталоги вида "...\1cv8".
    internal static string? ResolveForVersion(IEnumerable<string> oneCRoots, string platformVersion)
    {
        if (string.IsNullOrWhiteSpace(platformVersion))
        {
            return null;
        }

        foreach (var root in oneCRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var candidate = Path.Combine(root, platformVersion, "bin", ExeFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
