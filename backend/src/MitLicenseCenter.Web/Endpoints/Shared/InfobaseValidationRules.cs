using System.Text.RegularExpressions;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-022 — единый источник правил валидации Infobase/Publication на стороне бэка.
// Сюда вынесены regex версии платформы, max-длины полей и публикационная валидация,
// раньше продублированная между InfobasesEndpoints и PublicationsEndpoints. Оба эндпоинта
// и DataAnnotations DTO (InfobasesContracts/PublicationsContracts) ссылаются на эти
// константы. Человекочитаемая проза-спека правил — docs/03_DOMAIN_MODEL.md (§2, §3);
// литералы здесь закреплены parity-тестами (InfobasesValidationTests, FE validation.test.ts).
public static partial class InfobaseValidationRules
{
    // Max-длины полей (совпадают с nvarchar-констрейнтами БД и DTO-аннотациями).
    public const int NameMaxLength = 200;
    public const int DatabaseNameMaxLength = 200;
    public const int SiteNameMaxLength = 200;
    public const int VirtualPathMaxLength = 200;
    public const int PlatformVersionMaxLength = 50;
    public const int PhysicalPathMaxLength = 260;

    // Версия платформы 1С — 4 числовых сегмента «Major.Minor.Build.Revision».
    // Длины сегментов не фиксируем: реальные сборки бывают и «8.3.23.1865», и «8.5.1.1302».
    [GeneratedRegex(@"^\d+\.\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)]
    public static partial Regex PlatformVersionRegex();

    internal static bool IsValidPlatformVersion(string value) => PlatformVersionRegex().IsMatch(value ?? string.Empty);

    // Единая публикационная валидация (siteName / virtualPath / platformVersion / physicalPath).
    // prefix задаёт префикс ключей полей: "Publication." для вложенной публикации в
    // InfobasesEndpoints, "" — для прямого PUT /publications/{id} в PublicationsEndpoints.
    internal static void AppendPublicationFieldErrors(
        Dictionary<string, string[]> errors,
        string prefix,
        string? siteName,
        string? virtualPath,
        string? platformVersion,
        string? physicalPathOverride)
    {
        if (string.IsNullOrWhiteSpace(siteName))
        {
            errors[$"{prefix}SiteName"] = ["Укажите имя сайта IIS."];
        }

        var vp = (virtualPath ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(vp))
        {
            errors[$"{prefix}VirtualPath"] = ["Укажите виртуальный путь."];
        }
        else if (!vp.StartsWith('/'))
        {
            errors[$"{prefix}VirtualPath"] = ["Виртуальный путь должен начинаться с «/»."];
        }
        else if (vp.Any(char.IsWhiteSpace))
        {
            errors[$"{prefix}VirtualPath"] = ["Виртуальный путь не должен содержать пробелов."];
        }

        var version = (platformVersion ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(version))
        {
            errors[$"{prefix}PlatformVersion"] = ["Укажите версию платформы 1С."];
        }
        else if (!IsValidPlatformVersion(version))
        {
            errors[$"{prefix}PlatformVersion"] = ["Версия должна состоять из четырёх числовых сегментов, например «8.3.23.1865» или «8.5.1.1302»."];
        }

        // Physical-path override (PR 4.1): если задан — принимаем только абсолютные пути
        // (local C:\... или UNC \\server\share\...). Relative-пути отклоняем.
        if (!string.IsNullOrWhiteSpace(physicalPathOverride)
            && !Path.IsPathFullyQualified(physicalPathOverride.Trim()))
        {
            errors[$"{prefix}PhysicalPathOverride"] =
                ["Укажите абсолютный путь к папке (например, C:\\pub\\app или \\\\server\\share\\app)."];
        }
    }
}
