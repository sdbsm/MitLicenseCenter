using System.Text.RegularExpressions;

namespace MitLicenseCenter.Infrastructure.Ras;

// Парсеры строки запуска службы RAS (ImagePath из реестра: путь к ras.exe +
// аргументы). Работают на строке пути, источник её — реестр (ADR-47, Update
// MLC-162; раньше парсили вывод sc qc, теперь — ImagePath). Имя ras.exe в пути
// может быть в кавычках/без, любого регистра; версия — из сегмента ...\1cv8\<ver>\...;
// порт — из аргумента --port. Регэкспы переиспользованы как есть (логика на пути не
// изменилась при переходе с sc qc на реестр).
internal static partial class RasImagePathParser
{
    // Версия платформы 1С из пути: ...\1cv8\<N.N.N.N>\bin\ras.exe.
    [GeneratedRegex(@"[\\/]1cv8[\\/](?<version>\d+\.\d+\.\d+\.\d+)[\\/]", RegexOptions.IgnoreCase)]
    private static partial Regex PlatformFromPathRegex();

    // Порт RAS из аргументов: «--port=1545» или «--port 1545».
    [GeneratedRegex(@"--port[=\s]+(?<port>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PortRegex();

    // Содержит ли ImagePath ссылку на ras.exe (обнаружение службы по ImagePath, ADR-47).
    // Имя ras.exe может быть в кавычках/без, с любым регистром.
    public static bool ReferencesRas(string? imagePath)
        => !string.IsNullOrEmpty(imagePath)
           && imagePath.Contains("ras.exe", StringComparison.OrdinalIgnoreCase);

    // Версия платформы из пути (по сегменту ...\1cv8\<версия>\...). null, если путь
    // нестандартный.
    public static string? ParsePlatformVersion(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return null;
        }
        var m = PlatformFromPathRegex().Match(imagePath);
        return m.Success ? m.Groups["version"].Value : null;
    }

    // Порт RAS из аргументов в ImagePath. null, если флаг --port отсутствует (тогда
    // служба слушает дефолтный 1545 — это решает уровнем выше при сравнении с endpoint).
    public static string? ParsePort(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return null;
        }
        var m = PortRegex().Match(imagePath);
        return m.Success ? m.Groups["port"].Value : null;
    }
}
