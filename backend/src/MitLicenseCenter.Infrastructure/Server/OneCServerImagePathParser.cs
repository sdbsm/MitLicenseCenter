using System.Text.RegularExpressions;

namespace MitLicenseCenter.Infrastructure.Server;

// Парсеры строки запуска службы сервера 1С (ImagePath из реестра: путь к ragent.exe +
// аргументы). Работают на строке пути, источник — реестр (тот же приём, что
// RasImagePathParser для ras.exe, ADR-47/MLC-162). Имя ragent.exe может быть в
// кавычках/без, любого регистра; версия — из сегмента ...\<N.N.N.N>\bin\ragent.exe
// (например ...\8.3.23.1865\bin\ragent.exe). Свой парсер (не общий с RAS): у RAS версия
// привязана к сегменту \1cv8\<ver>\, у ragent надёжнее брать версию прямо перед
// \bin\ragent.exe — путь установки агента может отличаться от 1cv8.
internal static partial class OneCServerImagePathParser
{
    // Версия платформы 1С из пути: ...\<N.N.N.N>\bin\ragent.exe (имя exe в кавычках/без).
    [GeneratedRegex(
        @"[\\/](?<version>\d+\.\d+\.\d+\.\d+)[\\/]bin[\\/]""?ragent\.exe",
        RegexOptions.IgnoreCase)]
    private static partial Regex PlatformFromPathRegex();

    // Содержит ли ImagePath ссылку на ragent.exe (обнаружение службы сервера 1С).
    // Имя ragent.exe может быть в кавычках/без, с любым регистром.
    public static bool ReferencesRagent(string? imagePath)
        => !string.IsNullOrEmpty(imagePath)
           && imagePath.Contains("ragent.exe", StringComparison.OrdinalIgnoreCase);

    // Версия платформы из пути (по сегменту ...\<версия>\bin\ragent.exe). null, если путь
    // нестандартный (best-effort, как у RAS).
    public static string? ParsePlatformVersion(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return null;
        }
        var m = PlatformFromPathRegex().Match(imagePath);
        return m.Success ? m.Groups["version"].Value : null;
    }
}
