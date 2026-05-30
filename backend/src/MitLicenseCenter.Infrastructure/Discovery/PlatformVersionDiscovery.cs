using System.Text.RegularExpressions;
using MitLicenseCenter.Application.Discovery;

namespace MitLicenseCenter.Infrastructure.Discovery;

// Скан установленных версий платформы 1С: каталоги ...\1cv8\<версия>\bin.
// Имя каталога версии — четыре числовых сегмента (8.5.1.1302). Одна и та же
// версия может стоять и в x64-, и в x86-каталоге — тогда разрядности
// объединяются («x64, x86»). Возвращает версии по убыванию.
internal sealed partial class PlatformVersionDiscovery : IPlatformVersionDiscovery
{
    [GeneratedRegex(@"^\d+\.\d+\.\d+\.\d+$")]
    private static partial Regex VersionRegex();

    public IReadOnlyList<PlatformVersionInfo> FindPlatformVersions() =>
        Scan(OneCInstallRoots.GetWithArchitecture());

    // Ядро вынесено для unit-теста: принимает каталоги "...\1cv8" с разрядностью.
    internal static IReadOnlyList<PlatformVersionInfo> Scan(
        IEnumerable<(string Root, string Architecture)> roots)
    {
        // version → набор разрядностей (SortedSet → стабильный порядок "x64, x86").
        var byVersion = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (root, arch) in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (!VersionRegex().IsMatch(name)
                    || !Directory.Exists(Path.Combine(dir, "bin")))
                {
                    continue;
                }

                if (!byVersion.TryGetValue(name, out var archs))
                {
                    archs = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    byVersion[name] = archs;
                }
                archs.Add(arch);
            }
        }

        return byVersion
            .Select(kv => new PlatformVersionInfo(
                kv.Key,
                kv.Value.Count > 0 ? string.Join(", ", kv.Value) : null))
            // По убыванию версии (семантически, не строкой): 8.3.24 > 8.3.9.
            .OrderByDescending(v => Version.TryParse(v.Version, out var parsed)
                ? parsed
                : new Version(0, 0))
            .ToList();
    }
}
