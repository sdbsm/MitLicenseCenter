using System.Runtime.Versioning;
using Microsoft.Win32;
using MitLicenseCenter.Application.Discovery;

namespace MitLicenseCenter.Infrastructure.Discovery;

// Чтение локальных инстансов MSSQL из реестра:
// HKLM\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL — value-names
// этого ключа суть имена инстансов (MSSQLSERVER, SQLEXPRESS, …). Читаем оба view
// (64- и 32-битный, последний = WOW6432Node) ради 32-битных установок. Без SQL
// Browser / UDP-скана — только localhost. Платформо-зависимое чтение реестра
// отделено от чистого Map (как RacPathDiscovery.Scan).
internal sealed class SqlInstanceDiscovery : ISqlInstanceDiscovery
{
    public IReadOnlyList<string> FindLocalInstances()
    {
        // OperatingSystem.IsWindows() — platform-guard, признаётся анализатором
        // совместимости (CA1416): вызов реестра ниже легален без атрибута на обёртке.
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        return Map(ReadInstanceNames());
    }

    // Ядро вынесено для unit-теста: имена инстансов → серверные строки подключения.
    // MSSQLSERVER → "localhost"; именованный → "localhost\<name>". Дедуп (имена
    // инстансов регистронезависимы) + сортировка.
    internal static IReadOnlyList<string> Map(IEnumerable<string> instanceNames)
    {
        var servers = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in instanceNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var trimmed = name.Trim();
            servers.Add(string.Equals(trimmed, "MSSQLSERVER", StringComparison.OrdinalIgnoreCase)
                ? "localhost"
                : $@"localhost\{trimmed}");
        }

        return servers.ToList();
    }

    [SupportedOSPlatform("windows")]
    private static List<string> ReadInstanceNames()
    {
        const string subKey = @"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL";

        var names = new List<string>();
        // Registry64 + Registry32: 32-битный view = HKLM\SOFTWARE\WOW6432Node\…,
        // покрывает 32-битные установки SQL на 64-битной ОС.
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = baseKey.OpenSubKey(subKey);
            if (key is null)
            {
                continue;
            }

            names.AddRange(key.GetValueNames());
        }

        return names;
    }
}
