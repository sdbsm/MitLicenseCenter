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

        // Теги-обогатители (<plansql>/<dump>) — это глобальные директивы УРОВНЯ <config>, рядом с
        // <log>, а НЕ внутри него (шаблоны infostart 2020498/1431026; <dump>/<plansql>/<dbmslocks> —
        // config-level сборщики). Раньше они ошибочно клались внутрь <log> (MLC-245): план/дампы могли
        // не собираться. Порядок детей <config>: <dump> (если нужен) → <log> → <plansql> (если нужен).
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
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Неизвестный сценарий сбора ТЖ."),
    };

    // План запросов <plansql/> (config-level) — только для долгих запросов; по умолчанию НЕ собирается
    // (40_TECHLOG §6).
    private static bool NeedsPlanSql(TechLogScenario scenario) => scenario == TechLogScenario.SlowQueries;

    // Дамп аварий <dump/> (config-level) — только для исключений/падений (40_TECHLOG §6).
    private static bool NeedsDump(TechLogScenario scenario) => scenario == TechLogScenario.Exceptions;

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
