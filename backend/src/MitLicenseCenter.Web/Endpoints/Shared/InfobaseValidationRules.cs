using System.Buffers;
using System.Text.RegularExpressions;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-022 / MLC-118 — единый источник правил валидации Infobase/Publication на стороне бэка.
// Сюда вынесены regex версии платформы, max-длины полей, предикаты безопасности символов
// (connstr-/path-инъекции) и публикационная валидация, раньше продублированная между
// InfobasesEndpoints и PublicationsEndpoints. Оба эндпоинта и DataAnnotations DTO
// (InfobasesContracts/PublicationsContracts) ссылаются на эти константы. Человекочитаемая
// проза-спека правил — docs/03_DOMAIN_MODEL.md (§1.1, §3.5); литералы здесь закреплены
// parity-тестами (InfobasesValidationTests, FE validation.test.ts).
//
// Гоча CLAUDE.md: DataAnnotations ([StringLength]) на request-record'ах minimal API в
// рантайме НЕ валидируются — реальная защита идёт через ручные хелперы ниже. Поэтому
// длина и опасные символы проверяются здесь, а не только аннотациями DTO.
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

    // MLC-118 — наборы запрещённых символов (проза-спека: docs/03_DOMAIN_MODEL.md §1.1).
    // Кэшированные SearchValues<char> (CA1870) для быстрого IndexOfAny.
    // Infobase.Name уходит в Ref=<name> строки соединения webinst → запрет «; = "».
    private static readonly SearchValues<char> NameForbiddenChars = SearchValues.Create(";=\"");

    // Infobase.DatabaseName уходит в Path.Combine (подпапка бэкапа) и как SQL-идентификатор →
    // запрет служебных символов и символов пути.
    private static readonly SearchValues<char> DatabaseNameForbiddenChars =
        SearchValues.Create("\\/:*?\"<>|;'[]");

    // Publication.PhysicalPathOverride — абсолютный путь; «\ / :» легитимны, но connstr-/
    // мета-символы «; = "» запрещены (override может прорасти в connstr/командную строку).
    private static readonly SearchValues<char> PhysicalPathForbiddenChars = SearchValues.Create(";=\"");

    private static bool HasControlChar(string value)
    {
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }

    // MLC-118 / BE-07 / SEC-13 — Name безопасно ложится в connstr (Ref=<name>):
    // без управляющих символов и без «; = "». Над trimmed-значением.
    public static bool IsConnStrSafeName(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        return !HasControlChar(v) && v.IndexOfAny(NameForbiddenChars) < 0;
    }

    // MLC-118 / SEC-12 / UX-11 — DatabaseName без управляющих символов, без «..» и без
    // служебных/path-метасимволов. Над trimmed-значением.
    public static bool IsSafeDatabaseName(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        return !HasControlChar(v) && !v.Contains("..", StringComparison.Ordinal)
            && v.IndexOfAny(DatabaseNameForbiddenChars) < 0;
    }

    // MLC-118 / SEC-11 — VirtualPath без управляющих символов, без «\» и без «..»
    // (проверки начала с «/» и пробелов остаются в AppendPublicationFieldErrors).
    // Над trimmed-значением.
    public static bool IsSafeVirtualPath(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        return !HasControlChar(v) && !v.Contains('\\', StringComparison.Ordinal)
            && !v.Contains("..", StringComparison.Ordinal);
    }

    // MLC-118 / SEC-11 — PhysicalPathOverride без управляющих символов, без «..» и без
    // «; = "» (проверка абсолютности — Path.IsPathFullyQualified в AppendPublicationFieldErrors).
    // Над trimmed-значением.
    public static bool IsSafePhysicalPath(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        return !HasControlChar(v) && !v.Contains("..", StringComparison.Ordinal)
            && v.IndexOfAny(PhysicalPathForbiddenChars) < 0;
    }

    // MLC-118 — валидация полей самой инфобазы (Name/DatabaseName): required → длина →
    // символы; сообщается ПЕРВОЕ нарушение поля. Единый источник для ValidateInfobase
    // (InfobasesEndpoints, Create/Update) — тесты бьют по этому хелперу напрямую.
    internal static void AppendInfobaseFieldErrors(
        Dictionary<string, string[]> errors,
        string nameKey,
        string databaseNameKey,
        string? name,
        string? databaseName)
    {
        var n = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(n))
        {
            errors[nameKey] = ["Название инфобазы не может быть пустым."];
        }
        else if (n.Length > NameMaxLength)
        {
            errors[nameKey] = [$"Название не длиннее {NameMaxLength} символов."];
        }
        else if (!IsConnStrSafeName(n))
        {
            errors[nameKey] = ["Название не должно содержать символы «;», «=», «\"»."];
        }

        var db = (databaseName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(db))
        {
            errors[databaseNameKey] = ["Укажите имя БД."];
        }
        else if (db.Length > DatabaseNameMaxLength)
        {
            errors[databaseNameKey] = [$"Имя БД не длиннее {DatabaseNameMaxLength} символов."];
        }
        else if (!IsSafeDatabaseName(db))
        {
            errors[databaseNameKey] = ["Имя БД содержит недопустимые символы (служебные или символы пути)."];
        }
    }

    // Единая публикационная валидация (siteName / virtualPath / platformVersion / physicalPath).
    // prefix задаёт префикс ключей полей: "Publication." для вложенной публикации в
    // InfobasesEndpoints, "" — для прямого PUT /publications/{id} в PublicationsEndpoints.
    // Порядок проверок каждого поля: required → длина → формат/символы; сообщается ПЕРВОЕ нарушение.
    internal static void AppendPublicationFieldErrors(
        Dictionary<string, string[]> errors,
        string prefix,
        string? siteName,
        string? virtualPath,
        string? platformVersion,
        string? physicalPathOverride)
    {
        var site = (siteName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(site))
        {
            errors[$"{prefix}SiteName"] = ["Укажите имя сайта IIS."];
        }
        else if (site.Length > SiteNameMaxLength)
        {
            errors[$"{prefix}SiteName"] = [$"Значение не длиннее {SiteNameMaxLength} символов."];
        }

        var vp = (virtualPath ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(vp))
        {
            errors[$"{prefix}VirtualPath"] = ["Укажите виртуальный путь."];
        }
        else if (vp.Length > VirtualPathMaxLength)
        {
            errors[$"{prefix}VirtualPath"] = [$"Значение не длиннее {VirtualPathMaxLength} символов."];
        }
        else if (!vp.StartsWith('/'))
        {
            errors[$"{prefix}VirtualPath"] = ["Виртуальный путь должен начинаться с «/»."];
        }
        else if (vp.Any(char.IsWhiteSpace))
        {
            errors[$"{prefix}VirtualPath"] = ["Виртуальный путь не должен содержать пробелов."];
        }
        else if (!IsSafeVirtualPath(vp))
        {
            errors[$"{prefix}VirtualPath"] = ["Виртуальный путь содержит недопустимые символы."];
        }

        var version = (platformVersion ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(version))
        {
            errors[$"{prefix}PlatformVersion"] = ["Укажите версию платформы 1С."];
        }
        else if (version.Length > PlatformVersionMaxLength)
        {
            errors[$"{prefix}PlatformVersion"] = [$"Значение не длиннее {PlatformVersionMaxLength} символов."];
        }
        else if (!IsValidPlatformVersion(version))
        {
            errors[$"{prefix}PlatformVersion"] = ["Версия должна состоять из четырёх числовых сегментов, например «8.3.23.1865» или «8.5.1.1302»."];
        }

        // Physical-path override (PR 4.1): если задан — длина → абсолютность → символы.
        // Принимаем только абсолютные пути (local C:\... или UNC \\server\share\...);
        // relative-пути, «..», управляющие символы и «; = "» отклоняем (MLC-118 / SEC-11).
        if (!string.IsNullOrWhiteSpace(physicalPathOverride))
        {
            var pp = physicalPathOverride.Trim();
            if (pp.Length > PhysicalPathMaxLength)
            {
                errors[$"{prefix}PhysicalPathOverride"] = [$"Путь не длиннее {PhysicalPathMaxLength} символов."];
            }
            else if (!Path.IsPathFullyQualified(pp))
            {
                errors[$"{prefix}PhysicalPathOverride"] =
                    ["Укажите абсолютный путь к папке (например, C:\\pub\\app или \\\\server\\share\\app)."];
            }
            else if (!IsSafePhysicalPath(pp))
            {
                errors[$"{prefix}PhysicalPathOverride"] = ["Путь содержит недопустимые символы."];
            }
        }
    }
}
