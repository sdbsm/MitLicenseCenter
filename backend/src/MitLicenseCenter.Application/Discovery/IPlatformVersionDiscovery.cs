namespace MitLicenseCenter.Application.Discovery;

// Discovery установленных версий платформы 1С (папки ...\1cv8\<версия>\bin).
// Используется формой публикации вместо ручного ввода PlatformVersion.
public interface IPlatformVersionDiscovery
{
    // Возвращает версии, отсортированные по убыванию (свежие сверху). Чистый
    // скан ФС — не бросает исключений.
    IReadOnlyList<PlatformVersionInfo> FindPlatformVersions();
}

// Одна установленная версия платформы. Architecture — подсказка разрядности
// («x64», «x86» или «x64, x86», если версия установлена в обоих каталогах);
// null, если определить не удалось. Сохраняется в публикацию только Version.
public sealed record PlatformVersionInfo(string Version, string? Architecture);
