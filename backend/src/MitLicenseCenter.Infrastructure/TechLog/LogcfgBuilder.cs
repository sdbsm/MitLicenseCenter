using System.Globalization;
using System.Text;
using System.Xml.Linq;
using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Генератор целевого logcfg.xml (ядро MLC-230) — чистый C# без ФС, максимально юнит-тестируемый.
// Собирает XML по фактам стенда MLC-229 (40_TECHLOG §6/§8):
//   • format="json", location = каталог сбора, ограниченный history;
//   • НИКАКОГО фильтра по длительности (<ge property="Dur"/>) — он не работает для JSON-ТЖ 8.5;
//   • при заданном имени ИБ — <eq property="p:processName" value="<ИБ>"/> (изоляция арендатора);
//   • маркер-комментарий — по нему сторож отличает «наш» конфиг от чужого.
// Целевой namespace logcfg платформы 1С — http://v8.1c.ru/v8/tech-log.
internal sealed class LogcfgBuilder : ILogcfgBuilder
{
    // Маркер «нашего» конфига (XML-комментарий). Стабильная сигнатура — НЕ менять без миграции
    // сторожа (он распознаёт конфиг по подстроке Marker).
    public const string Marker = "managed by MitLicenseCenter";

    private const string TechLogNs = "http://v8.1c.ru/v8/tech-log";

    public string Build(TechLogScenario scenario, string? infobaseProcessName, string collectionLocation, int historyHours)
    {
        if (string.IsNullOrWhiteSpace(collectionLocation))
        {
            throw new ArgumentException("Каталог сбора ТЖ (location) обязателен.", nameof(collectionLocation));
        }

        XNamespace ns = TechLogNs;

        var log = new XElement(
            ns + "log",
            new XAttribute("location", collectionLocation),
            new XAttribute("history", historyHours.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("format", "json"));

        // Изоляция арендатора (60_SAFETY №2): при заданном имени ИБ — точный <eq> по p:processName.
        // НЕ <like> (в JSON ловит 0, 40_TECHLOG §6). Один фильтр на весь <log>: режет ВСЕ события.
        if (!string.IsNullOrWhiteSpace(infobaseProcessName))
        {
            log.Add(new XElement(
                ns + "eq",
                new XAttribute("property", "p:processName"),
                new XAttribute("value", infobaseProcessName)));
        }

        // Целевой набор событий по сценарию. БЕЗ фильтра длительности и без
        // <property name="all"/> — «целевой, не полный» сбор (60_SAFETY №1).
        foreach (var ev in EventsFor(scenario))
        {
            log.Add(new XElement(ns + "event", new XElement(ns + "eq",
                new XAttribute("property", "name"),
                new XAttribute("value", ev))));
        }

        // Целевые свойства lkX для сценария DbmsLocks (40_TECHLOG §5, infostart 1431026).
        // Только при этом сценарии — инвариант 60_SAFETY №1 «целевой, не полный» (не <property name="all"/>).
        // ⚠ Точная форма свойств в JSON-ТЖ 8.5 подлежит подтверждению на стенде (приёмка владельца).
        foreach (var prop in PropertiesFor(scenario))
        {
            log.Add(new XElement(ns + "property", new XAttribute("name", prop)));
        }

        // Теги-обогатители (<plansql>/<dump>/<dbmslocks>) — это глобальные директивы УРОВНЯ <config>,
        // рядом с <log>, а НЕ внутри него (шаблоны infostart 2020498/1431026; подтверждено MLC-245).
        // Раньше они ошибочно клались внутрь <log> (MLC-245): план/дампы могли не собираться.
        // Порядок детей <config>: <dump> (если нужен) → <log> → <plansql> (если нужен) → <dbmslocks> (если нужен).
        var config = new XElement(ns + "config");

        // Дамп аварий (сценарий Exceptions). Пишем в подкаталог каталога сбора → объём дампов учитывает
        // сторож размера каталога (MLC-231). ⚠ Тип/объём дампа (type) — за стенд-приёмкой: full-dump
        // может быть тяжёлым на проде (40_TECHLOG §6 «целевой, не полный»).
        if (NeedsDump(scenario))
        {
            config.Add(new XElement(
                ns + "dump",
                new XAttribute("location", collectionLocation.TrimEnd('\\', '/') + @"\dumps"),
                new XAttribute("create", "true"),
                new XAttribute("type", "3")));
        }

        config.Add(log);

        // План запросов (сценарий SlowQueries): config-level директива, по умолчанию НЕ собирается.
        if (NeedsPlanSql(scenario))
        {
            config.Add(new XElement(ns + "plansql"));
        }

        // СУБД-блокировки (сценарий DbmsLocks): config-level тег <dbmslocks/> (сиблинг <log>).
        // 40_TECHLOG §6: размещение на уровне <config>, как <plansql/> и <dump/> — шаблоны infostart 2020498/1431026.
        // ⚠ Точная семантика тега и структура полей lkX в JSON-ТЖ 8.5 подлежат подтверждению на стенде
        // (приёмка владельца). Объём без отборов → >6 ГБ/час (источник 1431026); короткое окно обязательно.
        if (NeedsDbmsLocks(scenario))
        {
            config.Add(new XElement(ns + "dbmslocks"));
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XComment($" {Marker}: scenario={scenario}; не редактировать вручную — управляется панелью "),
            config);

        return Serialize(doc);
    }

    public bool IsManaged(string? content)
        => !string.IsNullOrEmpty(content) && content.Contains(Marker, StringComparison.Ordinal);

    // Набор событий <event> под сценарий (40_TECHLOG §6). Все значения — точные имена событий ТЖ.
    private static string[] EventsFor(TechLogScenario scenario) => scenario switch
    {
        // Управляемые блокировки 1С + контекст транзакций (SDBL).
        TechLogScenario.Locks => new[] { "TLOCK", "TTIMEOUT", "TDEADLOCK", "SDBL" },
        // Долгие запросы к СУБД + модель данных 1С (порог длительности — в парсере, не тут).
        TechLogScenario.SlowQueries => new[] { "DBMSSQL", "SDBL" },
        // Исключения платформы + их контекст.
        TechLogScenario.Exceptions => new[] { "EXCP", "EXCPCNTX" },
        // Общая медленная серверная работа.
        TechLogScenario.GeneralSlow => new[] { "CALL", "DBMSSQL" },
        // СУБД-блокировки: поля lkX едут на событиях DBMSSQL (шаблон infostart 1431026).
        // 40_TECHLOG §5: lka/lkp — флаги источника/жертвы блокировки на уровне СУБД.
        TechLogScenario.DbmsLocks => new[] { "DBMSSQL" },
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Неизвестный сценарий сбора ТЖ."),
    };

    // Целевые свойства lkX внутри <log> (сценарий DbmsLocks, 40_TECHLOG §5, infostart 1431026).
    // Список — целевой (инвариант 60_SAFETY №1: никогда <property name="all"/>).
    // ⚠ Точные имена и состав полей в JSON-ТЖ 8.5 подлежат подтверждению на стенде (приёмка владельца).
    // Для остальных сценариев — пусто: поведение не меняется.
    private static string[] PropertiesFor(TechLogScenario scenario) => scenario switch
    {
        TechLogScenario.DbmsLocks => new[]
        {
            "lka",    // поток — источник блокировки (lka=1)
            "lkp",    // поток — жертва блокировки (lkp=1)
            "lkpid",  // номер запроса к СУБД у жертвы (кто её заблокировал)
            "lkaid",  // список номеров запросов у источника
            "lksrc",  // номер соединения источника (у жертвы) — связка «жертва → виновник»
            "lkpto",  // секунд с момента признания потока жертвой
            "lkato",  // секунд с момента признания потока источником
        },
        _ => Array.Empty<string>(),
    };

    // План запросов <plansql/> (config-level) — только для долгих запросов; по умолчанию НЕ собирается
    // (40_TECHLOG §6).
    private static bool NeedsPlanSql(TechLogScenario scenario) => scenario == TechLogScenario.SlowQueries;

    // Дамп аварий <dump/> (config-level) — только для исключений/падений (40_TECHLOG §6).
    private static bool NeedsDump(TechLogScenario scenario) => scenario == TechLogScenario.Exceptions;

    // СУБД-блокировки <dbmslocks/> (config-level) — только для сценария DbmsLocks (40_TECHLOG §6,
    // infostart 1431026). Сиблинг <log>, как <plansql/> и <dump/>.
    private static bool NeedsDbmsLocks(TechLogScenario scenario) => scenario == TechLogScenario.DbmsLocks;

    private static string Serialize(XDocument doc)
    {
        var sb = new StringBuilder();
        using var writer = new System.Xml.XmlTextWriter(new StringWriter(sb))
        {
            Formatting = System.Xml.Formatting.Indented,
            Indentation = 2,
        };
        doc.Save(writer);
        return sb.ToString();
    }
}
