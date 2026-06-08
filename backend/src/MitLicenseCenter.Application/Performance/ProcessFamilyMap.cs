namespace MitLicenseCenter.Application.Performance;

// Настраиваемый маппинг «имя процесса → семья» для атрибуции потребления ресурсов
// (MLC-064, ADR-26). Образец — LicenseConsumingAppIds: дефолт фиксируется здесь, оператор
// переопределяет список через dbo.Settings (Performance.ProcessFamilyMap) без редеплоя.
// Пустое/битое значение → Default, поэтому поведение по умолчанию детерминировано.
//
// Формат строки: семьи через «;», внутри «Имя=маска,маска,…».
//   OneC=rphost,ragent;Mssql=sqlservr
// Сопоставление — по ТОЧНОМУ имени процесса (без «.exe»), регистр не важен. Имя, не
// попавшее ни в одну семью, относится к OtherFamily — чистая функция, без WMI/Process.
public sealed class ProcessFamilyMap
{
    // Семья-остаток для всего, что не сматчилось (бэкап, ОС, пользовательские процессы).
    // Фронт локализует ключи семей (включая этот) через i18n.
    public const string OtherFamily = "Other";

    // 1С (rphost рабочие процессы / ragent агент / rmngr менеджер / ras сервер админ-API),
    // MSSQL (sqlservr), обновления Windows, Defender. Прочее → OtherFamily.
    public const string Default =
        "OneC=rphost,ragent,rmngr,ras;" +
        "Mssql=sqlservr;" +
        "OsUpdate=TiWorker,TrustedInstaller,wuauclt,usoclient,MoUsoCoreWorker;" +
        "Antivirus=MsMpEng,NisSrv";

    // Семьи в порядке объявления (первое совпадение выигрывает); маски — ordinal-ci set.
    private readonly IReadOnlyList<(string Family, IReadOnlySet<string> Masks)> _families;

    private ProcessFamilyMap(IReadOnlyList<(string, IReadOnlySet<string>)> families) => _families = families;

    public static ProcessFamilyMap Parse(string? raw)
    {
        var families = ParseGroups(string.IsNullOrWhiteSpace(raw) ? Default : raw);

        // Битая строка (ни одной валидной семьи) откатывается на дефолт — как
        // LicenseConsumingAppIds. Default — константа, валидна, рекурсия конечна.
        return families.Count > 0 ? new ProcessFamilyMap(families) : new ProcessFamilyMap(ParseGroups(Default));
    }

    // Имя процесса (без «.exe») → имя семьи. Первая семья, чьи маски содержат имя; иначе Other.
    public string Classify(string processName)
    {
        foreach (var (family, masks) in _families)
        {
            if (masks.Contains(processName))
            {
                return family;
            }
        }
        return OtherFamily;
    }

    private static List<(string, IReadOnlySet<string>)> ParseGroups(string source)
    {
        var families = new List<(string, IReadOnlySet<string>)>();
        foreach (var group in source.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = group.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var family = group[..eq].Trim();
            if (family.Length == 0)
            {
                continue;
            }

            var masks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mask in group[(eq + 1)..].Split(
                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                masks.Add(mask);
            }

            if (masks.Count > 0)
            {
                families.Add((family, masks));
            }
        }
        return families;
    }
}
