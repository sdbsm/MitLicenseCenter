namespace MitLicenseCenter.Infrastructure.Ras;

// Снимок зарегистрированной службы Windows из реестра: имя подключа
// (HKLM\SYSTEM\CurrentControlSet\Services\<name>) и развёрнутая строка ImagePath
// (путь к бинарю + аргументы). Достаточно для обнаружения службы RAS по ras.exe
// в ImagePath (ADR-47, Update MLC-162).
internal readonly record struct RegisteredService(string Name, string ImagePath);

// Чтение списка служб Windows из реестра. Вынесено за интерфейс, чтобы
// ScRasServiceManager обнаруживал службу RAS без обращения к реальному реестру в
// юнит-тестах: фейк подаёт заранее заготовленный список (включая реальный кейс
// «1C:Enterprise 8.5 Remote Server»). Production-реализация — тонкая, читает
// HKLM\SYSTEM\CurrentControlSet\Services и разворачивает REG_EXPAND_SZ ImagePath.
internal interface IServiceRegistryReader
{
    // Все службы, у которых удалось прочитать непустой ImagePath. Один проход по
    // реестру, без спавна процессов (чинит и ложное «не зарегистрирована», и
    // перф-риск перебора sc qc из ревью MLC-159).
    IReadOnlyList<RegisteredService> ReadServices();
}
