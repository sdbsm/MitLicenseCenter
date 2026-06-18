using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Infrastructure.Ras;

namespace MitLicenseCenter.Infrastructure.Server;

// Обнаружение служб сервера 1С (ragent) через реестр (MLC-213): один проход по
// IServiceRegistryReader.ReadServices() (HKLM\...\Services, без спавнов — как RAS,
// MLC-162), фильтр ImagePath по ragent.exe; для каждой совпавшей — состояние через
// IServiceStateReader и версия платформы из ImagePath. Несколько установленных версий
// платформы дают несколько служб ragent → возвращаем ВСЕ (в отличие от RAS, где берётся
// первая). Логика — на готовом списке служб, без прямого реестра: юнит-тест подаёт фейк.
internal sealed class OneCServerDiscovery : IOneCServerDiscovery
{
    private readonly IServiceRegistryReader _registry;
    private readonly IServiceStateReader _serviceState;

    public OneCServerDiscovery(IServiceRegistryReader registry, IServiceStateReader serviceState)
    {
        _registry = registry;
        _serviceState = serviceState;
    }

    public IReadOnlyList<OneCServerStatus> Discover()
    {
        var servers = new List<OneCServerStatus>();

        foreach (var svc in _registry.ReadServices())
        {
            if (!OneCServerImagePathParser.ReferencesRagent(svc.ImagePath))
            {
                continue;
            }

            var state = _serviceState.ReadState(svc.Name);

            servers.Add(new OneCServerStatus(
                ServiceName: svc.Name,
                Running: state?.IsRunning ?? false,
                PlatformVersion: OneCServerImagePathParser.ParsePlatformVersion(svc.ImagePath)));
        }

        return servers;
    }
}
