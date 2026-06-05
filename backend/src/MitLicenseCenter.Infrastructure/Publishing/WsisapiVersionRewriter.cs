using System.Text.RegularExpressions;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Pure-function helper (MLC-045): находит и заменяет version-сегмент в путях к
// wsisapi.dll — `…\<версия>\…\wsisapi.dll`. Версия 1С-платформы прописана в этом
// пути в web.config (современные сборки) и иногда в default.vrd (старые). Смена
// платформы = переписать ТОЛЬКО этот сегмент, не трогая остальную конфигурацию.
//
// Унаследовано из прежнего VrdPatcher (ADR-4.1, revoked): lookahead допускает любое
// число промежуточных path-сегментов между версией и wsisapi.dll; поиск привязан к
// "wsisapi.dll", иначе риск зацепить случайные четырёхсегментные числа.
internal static partial class WsisapiVersionRewriter
{
    [GeneratedRegex(
        @"(?<version>\d+\.\d+\.\d+\.\d+)(?=(?:[\\\/][^\\\/]*)*[\\\/][^\\\/]*wsisapi\.dll)",
        RegexOptions.IgnoreCase)]
    private static partial Regex WsisapiVersionRegex();

    // Версия из первого совпадения, либо null (путь к wsisapi.dll не найден).
    public static string? TryReadVersion(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;
        var m = WsisapiVersionRegex().Match(content);
        return m.Success ? m.Groups["version"].Value : null;
    }

    // Заменяет version-сегмент во всех путях к wsisapi.dll на newVersion. Если
    // совпадений нет — возвращает исходную строку без изменений (no-op, идемпотентно).
    public static string Rewrite(string content, string newVersion)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(newVersion);
        return WsisapiVersionRegex().Replace(content, newVersion);
    }
}
