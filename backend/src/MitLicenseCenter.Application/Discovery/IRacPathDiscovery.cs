namespace MitLicenseCenter.Application.Discovery;

// Discovery путей к rac.exe в стандартных каталогах установки платформы 1С.
// Используется страницей Settings вместо ручного ввода пути к OneC.RAS.ExePath.
public interface IRacPathDiscovery
{
    // Возвращает найденные пути к rac.exe (может быть пусто). Чистый скан ФС —
    // не бросает исключений на отсутствие каталогов.
    IReadOnlyList<string> FindRacExecutables();
}
