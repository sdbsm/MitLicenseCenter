namespace MitLicenseCenter.Application.Settings;

// Единый источник правды для whitelist'а client-типов 1С (app-id), потребляющих
// серверную лицензию. Дефолт фиксирует ADR-3.1/3.3 (PR 3.2); оператор переопределяет
// список через dbo.Settings (OneC.LicenseConsumingAppIds) без редеплоя. Пустое/незаданное
// значение → Default, поэтому поведение по умолчанию идентично прежнему статическому набору.
public static class LicenseConsumingAppIds
{
    public const string Default = "1CV8,1CV8C,WebClient,Designer,COMConnection";

    // Парсит запятую-разделённый список в case-insensitive набор. Пустой результат
    // (null/whitespace/только разделители) откатывается на Default — это гарантирует,
    // что незаданная настройка даёт ровно дефолтные app-id.
    public static HashSet<string> Parse(string? raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddSplit(set, raw);
        if (set.Count == 0)
        {
            AddSplit(set, Default);
        }
        return set;
    }

    private static void AddSplit(HashSet<string> set, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }
        foreach (var part in raw.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            set.Add(part);
        }
    }
}
