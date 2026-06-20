using System.Text.RegularExpressions;

namespace MitLicenseCenter.Application.TechLog;

// Общий хелпер нормализации p:processName к базовому имени инфобазы (MLC-234, этап B).
//
// Контекст (40_TECHLOG §8): поле p:processName в событиях ТЖ 8.5 несёт имя инфобазы, НО
// у фоновых/динамических сессий оно дополнено GUID-суффиксом:
//   • основная работа: «mitpro», «ut11_saratov» — голое имя ИБ;
//   • фоновые задания / динамические сессии: «ut11_saratov_296ee6d8-1234-...» — имя + суффикс.
// Нормализация = отсечение суффикса-GUID → базовое имя ИБ.
//
// Хелпер статический и чистый (без ФС/БД, без состояния), пригоден для любого
// анализатора слоя ТЖ, которому нужна привязка к арендатору (ISlowQueryAnalyzer и др.).
// Анализатор блокировок MLC-233 держит свою приватную копию логики — намеренно, чтобы не
// тянуть зависимость; унификация через этот файл — в последующей задаче (не в MLC-234).
public static partial class TechLogProcessName
{
    // Суффикс-GUID: «_<8>-<4>-<4>-<4>-<12>» в конце строки.
    // Снято со стенда 8.5.1.1302 (40_TECHLOG §8): «ut11_saratov_296ee6d8-1234-5678-abcd-...».
    [GeneratedRegex(
        @"_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex GuidSuffixRegex();

    // Нормализует p:processName к базовому имени ИБ: отсекает суффикс-GUID фоновых сессий.
    //   null / empty → возвращается как есть (толерантность — не бросаем).
    //   «infobase01» → «infobase01» (нет суффикса — без изменений).
    //   «infobase01_296ee6d8-1234-5678-abcd-ef0123456789» → «infobase01».
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        var m = GuidSuffixRegex().Match(raw);
        return m.Success ? raw[..m.Index] : raw;
    }
}
