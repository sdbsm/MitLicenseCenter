using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MitLicenseCenter.Infrastructure.Ras;

// Production-реализация IServiceRegistryReader: один проход по
// HKLM\SYSTEM\CurrentControlSet\Services. Для каждого подключа читаем ImagePath
// (REG_EXPAND_SZ → Environment.ExpandEnvironmentVariables), пропускаем службы без
// ImagePath (driver-ключи, group-ключи). Без спавна процессов — чинит ложное
// «не зарегистрирована» (sc query state= all из ArgumentList реальный sc.exe
// отклонял) и перф-риск перебора sc qc (ADR-47, Update MLC-162). Чтение реестра —
// тот же приём, что SqlInstanceDiscovery; платформо-зависимая часть отделена от
// логики обнаружения (она в ScRasServiceManager, на чистом списке).
internal sealed class RegistryServiceReader : IServiceRegistryReader
{
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

    public IReadOnlyList<RegisteredService> ReadServices()
    {
        // OperatingSystem.IsWindows() — platform-guard для CA1416 (как SqlInstanceDiscovery).
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        return ReadServicesWindows();
    }

    [SupportedOSPlatform("windows")]
    private static List<RegisteredService> ReadServicesWindows()
    {
        var services = new List<RegisteredService>();

        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var servicesKey = baseKey.OpenSubKey(ServicesKey);
        if (servicesKey is null)
        {
            return services;
        }

        foreach (var name in servicesKey.GetSubKeyNames())
        {
            RegistryKey? serviceKey = null;
            try
            {
                serviceKey = servicesKey.OpenSubKey(name);
                if (serviceKey is null)
                {
                    continue;
                }

                // RegistryValueOptions.None разворачивает REG_EXPAND_SZ автоматически, но
                // на части окружений значение приходит «сырым» (%SystemRoot%\…) — берём
                // без разворота и разворачиваем явно, чтобы фильтр по ras.exe не зависел
                // от настройки чтения.
                var raw = serviceKey.GetValue("ImagePath", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var imagePath = Environment.ExpandEnvironmentVariables(raw).Trim();
                if (imagePath.Length > 0)
                {
                    services.Add(new RegisteredService(name, imagePath));
                }
            }
            catch (System.Security.SecurityException)
            {
                // Нет прав на конкретный подключ — пропускаем (мы под SYSTEM, но защита
                // на случай нестандартных ACL отдельных служб).
            }
            finally
            {
                serviceKey?.Dispose();
            }
        }

        return services;
    }
}
