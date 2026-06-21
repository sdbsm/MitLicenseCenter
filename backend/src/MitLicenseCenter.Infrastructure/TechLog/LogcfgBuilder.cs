using System.Globalization;
using System.Text;
using System.Xml.Linq;
using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Генератор целевого logcfg.xml (ядро MLC-230; сверка с офиц. спекой 41_LOGCFG_SPEC §4 — MLC-246).
// Чистый C# без ФС, максимально юнит-тестируемый. Собирает XML по официальной модели <event>/<property>:
//   • format="json", location = каталог сбора, ограниченный history;
//   • НИКАКОГО фильтра по длительности (<ge property="Dur"/>) — свойство звалось "Dur", его нет в перечне
//     (41_LOGCFG_SPEC §5: фильтр — Duration/Durationus, не Dur); ретест с Duration за стендом (F-3).
//     До ретеста порог длительности делает парсер (этап B), фильтр в logcfg не ставим;
//   • при заданном имени ИБ — <eq property="p:processName"> лежит ВНУТРИ КАЖДОГО <event>, рядом с
//     условием name (объединение по «И» внутри <event>, 41_LOGCFG_SPEC §4.1) — изоляция арендатора
//     (60_SAFETY №2 = event-scope). НЕ прямым ребёнком <log> (структурно недопустимо, игнорируется → утечка);
//   • <property name="all"/> в <log> — иначе свойства событий НЕ пишутся вовсе (41_LOGCFG_SPEC §4.2:
//     «свойство выводится, ТОЛЬКО если есть элемент <property>») → парсер этапа B получил бы пусто.
//     Это property-scope (все поля УЖЕ отобранных событий), НЕ «полный ТЖ»: event-scope режут фильтры
//     типа события + p:processName (60_SAFETY №1);
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

        // Guard history≥1 (MLC-247 B3, 41_LOGCFG_SPEC §3): history=0 = «НЕ удалять» — опасно
        // (переполнение диска). Валидация Min=1 есть в настройках (TechLog.HistoryHours), но генератор
        // обязан быть защищён сам (контракт «никогда не генерируем небезопасный конфиг»).
        if (historyHours < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(historyHours), historyHours,
                "history (часы) должен быть ≥ 1 — 0 означает 'не удалять' (переполнение диска), спека §3.");
        }

        XNamespace ns = TechLogNs;

        var log = new XElement(
            ns + "log",
            new XAttribute("location", collectionLocation),
            new XAttribute("history", historyHours.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("format", "json"));

        // Изоляция арендатора (60_SAFETY №2, event-scope): при заданном имени ИБ условие
        // <eq property="p:processName"> кладётся ВНУТРЬ КАЖДОГО <event>, рядом с условием name —
        // объединение по «И» внутри одного <event> (41_LOGCFG_SPEC §4.1). НЕ прямым ребёнком <log>
        // (структурно недопустимо → игнорируется платформой → собирался бы ТЖ ВСЕХ арендаторов: утечка).
        // НЕ <like> (в JSON ловит 0, 40_TECHLOG §6) — точный <eq>.
        var isolated = !string.IsNullOrWhiteSpace(infobaseProcessName);
        var tenantProcessName = infobaseProcessName ?? string.Empty;

        // Целевой набор событий по сценарию (event-scope: тип события [+ арендатор]).
        foreach (var ev in EventsFor(scenario))
        {
            var evElement = new XElement(ns + "event", new XElement(ns + "eq",
                new XAttribute("property", "name"),
                new XAttribute("value", ev)));

            if (isolated)
            {
                evElement.Add(new XElement(ns + "eq",
                    new XAttribute("property", "p:processName"),
                    new XAttribute("value", tenantProcessName)));
            }

            log.Add(evElement);
        }

        // Свойства событий (property-scope): без <property> платформа пишет события ЛИШЬ с базовыми
        // полями (ts/duration/name/depth/level/process) — без Sql/Context/Locks/Descr/p:processName/…,
        // и анализаторы этапа B получили бы пусто (41_LOGCFG_SPEC §4.2). <property name="all"/> пишет
        // все свойства УЖЕ ОТОБРАННЫХ (по типу события + арендатору) событий — это property-scope, а
        // НЕ «полный ТЖ»: инвариант 60_SAFETY №1 «никогда полный ТЖ» про event-scope (фильтр события +
        // p:processName), который тут целевой. Робастно к «полям-призракам» и вариантам имён (Usr/UserName).
        log.Add(new XElement(ns + "property", new XAttribute("name", "all")));

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
        // Сам план попадает в свойство planSQLText события СУБД — оно пишется благодаря <property name="all"/>
        // выше (41_LOGCFG_SPEC §7: без <property name="planSQLText"/>/sql план в журнал не записывается).
        if (NeedsPlanSql(scenario))
        {
            config.Add(new XElement(ns + "plansql"));
        }

        // СУБД-блокировки (сценарий DbmsLocks): config-level тег <dbmslocks/> (сиблинг <log>, как
        // <plansql>/<dump>). Включает формирование полей lkX на событиях СУБД (41_LOGCFG_SPEC §8,
        // infostart 1431026); сами поля выводятся благодаря <property name="all"/> выше — отдельные
        // <property name="lkX"/> НЕ нужны. ⚠ Объём без отборов по длительности → >6 ГБ/час (1431026):
        // короткое окно + лимит места (MLC-231) обязательны. Точная семантика тега и форма lkX в
        // JSON-ТЖ 8.5 — за стенд-приёмкой.
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
        // СУБД-блокировки: поля lkX едут на событиях DBMSSQL (тег <dbmslocks/> их формирует,
        // 41_LOGCFG_SPEC §8, infostart 1431026).
        TechLogScenario.DbmsLocks => new[] { "DBMSSQL" },
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Неизвестный сценарий сбора ТЖ."),
    };

    // План запросов <plansql/> (config-level) — только для долгих запросов; по умолчанию НЕ собирается
    // (40_TECHLOG §6).
    private static bool NeedsPlanSql(TechLogScenario scenario) => scenario == TechLogScenario.SlowQueries;

    // Дамп аварий <dump/> (config-level) — только для исключений/падений (40_TECHLOG §6).
    private static bool NeedsDump(TechLogScenario scenario) => scenario == TechLogScenario.Exceptions;

    // СУБД-блокировки <dbmslocks/> (config-level) — только для сценария DbmsLocks (40_TECHLOG §6,
    // 41_LOGCFG_SPEC §8, infostart 1431026). Сиблинг <log>, как <plansql/> и <dump/>.
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
