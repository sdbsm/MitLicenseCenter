using System.Text.RegularExpressions;
using System.Xml.Linq;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Infrastructure.Publishing;

// Pure-function helper: вся XML-логика default.vrd живёт здесь, чтобы
// unit-тесты могли её покрыть без файловой системы и без ServerManager.
// Реальная I/O — в OneCIisPublishingService поверх этого хелпера.
//
// Контракт surgical-patch (см. ADR-4.1 в docs/DECISIONS.md):
//   1) <standardOdata enable="..."/> и <httpServices publishByDefault="..."/> —
//      toggle attribute без замены ноды целиком (если ноды нет — она создаётся
//      с минимально необходимым набором атрибутов).
//   2) wsisapi.dll path — заменяется ТОЛЬКО version-сегмент `8\.3\.\d+\.\d+`
//      на `publication.PlatformVersion`. Если такой строки в файле нет
//      (новые 1C-сборки выносят handler в web.config) — операция no-op.
//   3) publication.VrdCustomXml — overlay стратегией «replace-child-by-localname,
//      append-if-missing» (см. ADR-4.1). Никогда не дропаем пользовательские
//      child-ноды, которых нет в VrdCustomXml.
internal static class VrdPatcher
{
    // Namespace 1C VRD-формата. Зафиксирован стабильно с 8.3+.
    private static readonly XNamespace VrdNs = "http://v8.1c.ru/8.2/virtual-resource-system";

    // Regex выделяет только version-сегмент в путях вида
    //   C:\Program Files\1cv8\8.3.23.1865\bin\wsisapi.dll
    // Lookahead допускает любое количество промежуточных path-сегментов между
    // версией и wsisapi.dll (исторически между ними `\bin\`, но в разных
    // сборках бывает по-разному). Поиск ведётся только в attribute values,
    // содержащих "wsisapi.dll" — иначе риск ложно сцепить случайные числа.
    private static readonly Regex WsisapiVersionRegex = new(
        @"(?<version>\d+\.\d+\.\d+\.\d+)(?=(?:[\\\/][^\\\/]*)*[\\\/][^\\\/]*wsisapi\.dll)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Patch(string vrdXml, Publication desired)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vrdXml);
        ArgumentNullException.ThrowIfNull(desired);

        var doc = XDocument.Parse(vrdXml, LoadOptions.PreserveWhitespace);
        var root = doc.Root ?? throw new InvalidOperationException("default.vrd: пустой документ (нет корневого элемента).");

        ToggleAttribute(root, "standardOdata", "enable", desired.EnableOData);
        ToggleAttribute(root, "httpServices", "publishByDefault", desired.EnableHttpServices);
        PatchPlatformVersion(doc, desired.PlatformVersion);

        if (!string.IsNullOrWhiteSpace(desired.VrdCustomXml))
        {
            MergeCustomOverlay(root, desired.VrdCustomXml);
        }

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    public static string? TryReadPlatformVersion(string vrdXml)
    {
        if (string.IsNullOrWhiteSpace(vrdXml))
            return null;
        var m = WsisapiVersionRegex.Match(vrdXml);
        return m.Success ? m.Groups["version"].Value : null;
    }

    public static bool TryReadODataEnabled(string vrdXml, out bool enabled)
    {
        enabled = false;
        try
        {
            var doc = XDocument.Parse(vrdXml);
            var root = doc.Root;
            if (root is null) return false;
            var node = FindLocal(root, "standardOdata");
            if (node is null) return false;
            enabled = ParseBoolAttribute(node, "enable", defaultValue: false);
            return true;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    public static bool TryReadHttpServicesEnabled(string vrdXml, out bool enabled)
    {
        enabled = false;
        try
        {
            var doc = XDocument.Parse(vrdXml);
            var root = doc.Root;
            if (root is null) return false;
            var node = FindLocal(root, "httpServices");
            if (node is null) return false;
            enabled = ParseBoolAttribute(node, "publishByDefault", defaultValue: false);
            return true;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    private static void ToggleAttribute(XElement root, string localName, string attributeName, bool value)
    {
        var node = FindLocal(root, localName);
        var literal = value ? "true" : "false";
        if (node is null)
        {
            // Узла нет — создаём минимально достаточный (в дефолтном namespace
            // VRD, чтобы 1C-платформа корректно прочитала). Не пытаемся
            // «угадать» дополнительные атрибуты — оператор может дописать их
            // через VrdCustomXml.
            var name = root.Name.Namespace == XNamespace.None
                ? XName.Get(localName)
                : root.Name.Namespace + localName;
            root.Add(new XElement(name, new XAttribute(attributeName, literal)));
            return;
        }
        node.SetAttributeValue(attributeName, literal);
    }

    private static void PatchPlatformVersion(XDocument doc, string platformVersion)
    {
        if (string.IsNullOrWhiteSpace(platformVersion))
            return;

        // Идём по строковым атрибутам, в которых упомянут wsisapi.dll —
        // исторически `<isapi path="..."/>`, в кастомных нодах оператора через
        // VrdCustomXml, и т.д. Версия может стоять на любом уровне пути; regex
        // вылавливает только её, prefix/suffix не трогаем. Жёсткое условие
        // `Contains("wsisapi.dll")` гарантирует, что случайное «8.3.x.x» в
        // другом атрибуте (комментарий, version-tag) не будет переписано.
        foreach (var attr in doc.Descendants().SelectMany(e => e.Attributes()).ToList())
        {
            if (!attr.Value.Contains("wsisapi.dll", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!WsisapiVersionRegex.IsMatch(attr.Value))
                continue;
            attr.Value = WsisapiVersionRegex.Replace(attr.Value, platformVersion);
        }
    }

    private static void MergeCustomOverlay(XElement root, string customXml)
    {
        // Оборачиваем custom-фрагмент в pseudo-root, чтобы XDocument.Parse
        // принял как multiple top-level elements (1C-операторы пишут VrdCustomXml
        // как список child-нод без общего родителя).
        var wrapped = $"<__overlay xmlns=\"{root.Name.NamespaceName}\">{customXml}</__overlay>";
        XDocument overlayDoc;
        try
        {
            overlayDoc = XDocument.Parse(wrapped);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidOperationException(
                $"VrdCustomXml содержит некорректный XML: {ex.Message}", ex);
        }

        var overlayRoot = overlayDoc.Root!;
        foreach (var child in overlayRoot.Elements())
        {
            // Стратегия (ADR-4.1): replace-child-by-localname, иначе append.
            // Match по LocalName, чтобы override работал независимо от того,
            // в каком namespace оператор написал кастом (а в реальности почти
            // всегда — без явного xmlns, тогда наш wrap-namespace инжектится).
            var existing = FindLocal(root, child.Name.LocalName);
            if (existing is not null)
            {
                existing.ReplaceWith(child);
            }
            else
            {
                root.Add(child);
            }
        }
    }

    private static XElement? FindLocal(XElement root, string localName) =>
        root.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.Ordinal));

    private static bool ParseBoolAttribute(XElement node, string attrName, bool defaultValue)
    {
        var raw = node.Attribute(attrName)?.Value;
        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "1", StringComparison.Ordinal);
    }
}
