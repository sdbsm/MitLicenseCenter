using MitLicenseCenter.Application.Server;

namespace MitLicenseCenter.Infrastructure.Server;

// Обнаружение служб сервера 1С (ragent) на локальном узле (MLC-213). Internal в
// Infrastructure — наружу (Application/Web) отдаётся только IServerStatusProvider +
// IWindowsServiceController. Обнаружение по ImagePath, содержащему ragent.exe (имя самой
// службы у операторов не стандартизировано — как у RAS, ADR-47); несколько установленных
// версий платформы → несколько служб. Чистая логика на готовом списке служб
// (IServiceRegistryReader), без прямого реестра — чтобы юнит-тест подавал фейковый список.
internal interface IOneCServerDiscovery
{
    // Все обнаруженные службы ragent с верифицированным состоянием (running) и версией
    // платформы из ImagePath (best-effort). Пустой список — служб ragent нет. Может
    // бросить при недоступности реестра — вызывающий провайдер ловит (Available/деградация).
    IReadOnlyList<OneCServerStatus> Discover();
}
