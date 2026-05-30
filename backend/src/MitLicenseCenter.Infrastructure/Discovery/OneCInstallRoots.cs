namespace MitLicenseCenter.Infrastructure.Discovery;

// Общий источник корневых каталогов установки платформы 1С (...\1cv8).
// Используется и сканом rac.exe, и сканом версий платформы. Разрядность
// выводится из корня: ProgramW6432/ProgramFiles → x64, ProgramFiles(x86) → x86.
internal static class OneCInstallRoots
{
    public static IEnumerable<string> Get() => GetWithArchitecture().Select(x => x.Root);

    public static IEnumerable<(string Root, string Architecture)> GetWithArchitecture()
    {
        // Дедуп по пути: в 64-бит процессе ProgramW6432 == ProgramFiles — отдадим
        // один раз как x64. ProgramFiles(x86) — отдельный каталог, помечаем x86.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (env, arch) in new[]
                 {
                     ("ProgramW6432", "x64"),
                     ("ProgramFiles", "x64"),
                     ("ProgramFiles(x86)", "x86"),
                 })
        {
            var programFiles = Environment.GetEnvironmentVariable(env);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                var root = Path.Combine(programFiles, "1cv8");
                if (seen.Add(root))
                {
                    yield return (root, arch);
                }
            }
        }
    }
}
