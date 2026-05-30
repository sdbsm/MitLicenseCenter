using MitLicenseCenter.Application.Discovery;

namespace MitLicenseCenter.Infrastructure.Discovery;

// Скан стандартных каталогов установки 1С на наличие rac.exe:
// {ProgramFiles}\1cv8\{version}\bin\rac.exe. Чистая работа с ФС, без процессов.
// Возвращает только пути к файлам.
internal sealed class RacPathDiscovery : IRacPathDiscovery
{
    public IReadOnlyList<string> FindRacExecutables() => Scan(OneCInstallRoots.Get());

    // Ядро вынесено для unit-теста: принимает каталоги вида "...\1cv8".
    internal static IReadOnlyList<string> Scan(IEnumerable<string> oneCRoots)
    {
        var found = new List<string>();

        foreach (var root in oneCRoots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            foreach (var versionDir in Directory.EnumerateDirectories(root))
            {
                var candidate = Path.Combine(versionDir, "bin", "rac.exe");
                if (File.Exists(candidate)
                    && !found.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    found.Add(candidate);
                }
            }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }
}
