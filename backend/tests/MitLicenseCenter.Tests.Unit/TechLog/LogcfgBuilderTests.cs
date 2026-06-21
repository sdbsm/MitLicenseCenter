using System.Xml.Linq;
using FluentAssertions;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Infrastructure.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-230 (ADR-57/58) + сверка с офиц. спекой MLC-246: генератор целевого logcfg.xml — ядро задачи,
// чистый C#. Тесты кодируют СЕМАНТИКУ модели <event>/<property> (41_LOGCFG_SPEC §4), а не только
// XML-структуру (слабые структурные тесты и пропустили баги F-1/F-2): format="json", НИКАКОГО фильтра
// длительности, изоляция арендатора <eq p:processName> ВНУТРИ каждого <event> (F-1), обязательный
// <property name="all"/> для записи свойств событий (F-2), event-scope строго целевой, маркер.
public sealed class LogcfgBuilderTests
{
    private const string Location = @"C:\ProgramData\MitLicenseCenter\techlog";
    private readonly LogcfgBuilder _builder = new();

    private XDocument BuildDoc(
        TechLogScenario scenario = TechLogScenario.Locks, string? infobase = null, int history = 2)
        => XDocument.Parse(_builder.Build(scenario, infobase, Location, historyHours: history));

    [Fact]
    public void Build_sets_json_format_location_and_history_on_log()
    {
        var xml = _builder.Build(TechLogScenario.Locks, null, Location, historyHours: 3);
        var doc = XDocument.Parse(xml);
        var log = doc.Descendants().Single(e => e.Name.LocalName == "log");

        log.Attribute("format")!.Value.Should().Be("json");
        log.Attribute("location")!.Value.Should().Be(Location);
        log.Attribute("history")!.Value.Should().Be("3");
    }

    [Fact]
    public void Build_never_emits_duration_filter()
    {
        // F-3 (MLC-246): фильтр длительности в logcfg НЕ ставим. Прежний прогон с property="Dur" ловил 0,
        // но "Dur" — НЕсуществующее свойство (41_LOGCFG_SPEC §5: фильтр — Duration/Durationus). Верный ли
        // фильтр (<ge property="Duration"/>) работает в JSON — за стенд-ретестом; до него порог длительности
        // делает парсер (этап B), а в logcfg фильтра нет (убрать безопасно). Тест закрепляет отсутствие.
        foreach (var scenario in Enum.GetValues<TechLogScenario>())
        {
            var xml = _builder.Build(scenario, "mitpro", Location, 2);
            xml.Should().NotContain("<ge", $"сценарий {scenario}: запрещён фильтр <ge> по длительности");
            xml.Should().NotContain("Dur", $"сценарий {scenario}: запрещено свойство Dur");
            xml.Should().NotContain("duration", $"сценарий {scenario}: запрещён фильтр duration");
        }
    }

    [Fact]
    public void Build_isolation_processName_lives_inside_each_event_next_to_name()
    {
        // F-1 (MLC-246): изоляция арендатора — это СЕМАНТИКА event-scope (41_LOGCFG_SPEC §4.1), а не
        // «есть где-то <eq p:processName>». Условие p:processName обязано лежать ВНУТРИ <event> (его
        // родитель — <event>), и в КАЖДОМ <event> должны быть И name, И p:processName (объединение по «И»).
        var doc = BuildDoc(TechLogScenario.SlowQueries, infobase: "ut11_saratov");
        var events = doc.Descendants().Where(e => e.Name.LocalName == "event").ToArray();
        events.Should().NotBeEmpty();

        foreach (var ev in events)
        {
            var props = ev.Elements()
                .Where(c => c.Name.LocalName == "eq")
                .Select(c => c.Attribute("property")!.Value)
                .ToArray();
            props.Should().Contain("name", "каждое <event> отбирает по типу события");
            props.Should().Contain("p:processName", "каждое <event> изолировано по арендатору (И с name)");
        }

        // Каждый <eq p:processName> — прямой ребёнок <event> (НЕ прямой ребёнок <log>), значение = имя ИБ.
        var processNameEqs = doc.Descendants()
            .Where(e => e.Name.LocalName == "eq" && e.Attribute("property")?.Value == "p:processName")
            .ToArray();
        processNameEqs.Should().HaveCount(events.Length, "ровно один фильтр арендатора на каждое <event>");
        processNameEqs.Should().OnlyContain(eq => eq.Parent!.Name.LocalName == "event",
            "p:processName лежит ВНУТРИ <event>, а не прямым ребёнком <log> (иначе игнорируется → утечка)");
        processNameEqs.Should().OnlyContain(eq => eq.Attribute("value")!.Value == "ut11_saratov");
    }

    [Fact]
    public void Build_never_uses_like_for_isolation()
    {
        // <like ...%> в JSON НЕ работает (40_TECHLOG §6) — обход НЕ применяется, только точный <eq>.
        var xml = _builder.Build(TechLogScenario.SlowQueries, "mitpro", Location, 2);
        xml.Should().NotContain("<like");
    }

    [Fact]
    public void Build_without_infobase_has_no_processName_filter()
    {
        // null = весь кластер: фильтра по ИБ нет (но это осознанный выбор, см. сервис/60_SAFETY).
        var doc = BuildDoc(infobase: null);
        doc.Descendants()
            .Where(e => e.Name.LocalName == "eq")
            .Any(e => e.Attribute("property")?.Value == "p:processName")
            .Should().BeFalse();
    }

    [Fact]
    public void Build_emits_all_property_inside_log_so_event_properties_are_written()
    {
        // F-2 (MLC-246): без <property> платформа пишет события лишь с базовыми полями (41_LOGCFG_SPEC
        // §4.2) → анализаторы B получают пусто. <property name="all"/> внутри <log> = property-scope:
        // все свойства УЖЕ ОТОБРАННЫХ событий. Это НЕ «полный ТЖ» (тот режется event-scope ниже).
        foreach (var scenario in Enum.GetValues<TechLogScenario>())
        {
            var doc = XDocument.Parse(_builder.Build(scenario, null, Location, 2));
            var log = doc.Descendants().Single(e => e.Name.LocalName == "log");
            var allProperty = log.Elements()
                .Where(e => e.Name.LocalName == "property")
                .SingleOrDefault(e => e.Attribute("name")?.Value == "all");

            allProperty.Should().NotBeNull(
                $"сценарий {scenario}: <property name=\"all\"/> обязателен внутри <log>, иначе свойства не пишутся");
        }
    }

    [Fact]
    public void Build_keeps_event_scope_targeted_never_full_techlog()
    {
        // F-2 граница (MLC-246): property-scope полон (all), но EVENT-scope строго целевой — инвариант
        // 60_SAFETY №1 «никогда полный ТЖ» = никогда <ne> по name и события строго из сценария
        // (никогда <property name="name" value="all"> и т.п.). Объём режут тип события + арендатор.
        foreach (var scenario in Enum.GetValues<TechLogScenario>())
        {
            var doc = XDocument.Parse(_builder.Build(scenario, "ut11_saratov", Location, 2));

            // Никаких <ne> (отрицаний) — это путь к «всё, кроме» = полному ТЖ.
            doc.Descendants().Any(e => e.Name.LocalName == "ne")
                .Should().BeFalse($"сценарий {scenario}: <ne> запрещён (ведёт к полному ТЖ)");

            // Каждое <event> отбирает по конкретному имени из EventsFor — никаких «все события».
            var selectedNames = doc.Descendants()
                .Where(e => e.Name.LocalName == "event")
                .SelectMany(e => e.Elements().Where(c => c.Name.LocalName == "eq"))
                .Where(eq => eq.Attribute("property")?.Value == "name")
                .Select(eq => eq.Attribute("value")!.Value)
                .ToArray();
            selectedNames.Should().NotBeEmpty($"сценарий {scenario}: события отбираются по name, а не «все»");
            selectedNames.Should().OnlyContain(n => n != "all" && !string.IsNullOrEmpty(n));
        }
    }

    [Theory]
    [InlineData(TechLogScenario.Locks, new[] { "TLOCK", "TTIMEOUT", "TDEADLOCK", "SDBL" })]
    [InlineData(TechLogScenario.SlowQueries, new[] { "DBMSSQL", "SDBL" })]
    [InlineData(TechLogScenario.Exceptions, new[] { "EXCP", "EXCPCNTX" })]
    [InlineData(TechLogScenario.GeneralSlow, new[] { "CALL", "DBMSSQL" })]
    [InlineData(TechLogScenario.DbmsLocks, new[] { "DBMSSQL" })]
    public void Build_selects_events_by_scenario(TechLogScenario scenario, string[] expectedEvents)
    {
        var doc = BuildDoc(scenario);
        var events = doc.Descendants()
            .Where(e => e.Name.LocalName == "event")
            .SelectMany(e => e.Descendants().Where(c => c.Name.LocalName == "eq"))
            .Where(eq => eq.Attribute("property")?.Value == "name")
            .Select(eq => eq.Attribute("value")!.Value)
            .ToArray();

        events.Should().BeEquivalentTo(expectedEvents);
    }

    [Fact]
    public void Build_slow_queries_includes_plansql_tag_at_config_level()
    {
        // Планы запросов по умолчанию НЕ собираются — нужен явный тег <plansql/> (40_TECHLOG §6).
        // MLC-245: <plansql/> — config-level директива (ребёнок <config>, рядом с <log>), НЕ внутри
        // <log> (шаблоны infostart 2020498/1431026) — иначе план может не собираться.
        var plansql = BuildDoc(TechLogScenario.SlowQueries)
            .Descendants().SingleOrDefault(e => e.Name.LocalName == "plansql");

        plansql.Should().NotBeNull();
        plansql!.Parent!.Name.LocalName.Should().Be("config", "<plansql/> обязан быть на уровне <config>");
    }

    [Fact]
    public void Build_exceptions_includes_dump_tag_at_config_level_with_attributes()
    {
        // MLC-245: <dump/> — config-level директива с валидными атрибутами (location/create/type),
        // а не пустой тег внутри <log> (шаблоны infostart 2020498/1431026, 40_TECHLOG §6).
        var dump = BuildDoc(TechLogScenario.Exceptions)
            .Descendants().SingleOrDefault(e => e.Name.LocalName == "dump");

        dump.Should().NotBeNull();
        dump!.Parent!.Name.LocalName.Should().Be("config", "<dump/> обязан быть на уровне <config>");
        dump.Attribute("location")!.Value.Should().Contain("dumps", "дампы пишем в подкаталог каталога сбора");
        dump.Attribute("create")!.Value.Should().Be("true");
        dump.Attribute("type").Should().NotBeNull("у <dump/> должен быть тип дампа");
    }

    [Fact]
    public void Build_never_nests_enricher_tags_inside_log()
    {
        // Регресс MLC-245/MLC-236: <plansql>/<dump>/<dbmslocks> не должны лежать ВНУТРИ <log> ни в одном
        // сценарии — это config-level директивы (сиблинги <log>).
        foreach (var scenario in Enum.GetValues<TechLogScenario>())
        {
            var doc = BuildDoc(scenario);
            var log = doc.Descendants().Single(e => e.Name.LocalName == "log");
            log.Descendants().Any(e => e.Name.LocalName is "plansql" or "dump" or "dbmslocks")
                .Should().BeFalse($"сценарий {scenario}: теги-обогатители не внутри <log>");
        }
    }

    [Fact]
    public void Build_dbms_locks_emits_config_level_dbmslocks_tag()
    {
        // MLC-236: сценарий DbmsLocks включает config-level тег <dbmslocks/> (сиблинг <log>), который
        // формирует поля lkX на событиях СУБД (41_LOGCFG_SPEC §8, infostart 1431026). Поля выводятся
        // благодаря <property name="all"/> (MLC-246) — отдельные <property name="lkX"/> не нужны.
        var doc = BuildDoc(TechLogScenario.DbmsLocks);

        var dbmslocks = doc.Descendants().SingleOrDefault(e => e.Name.LocalName == "dbmslocks");
        dbmslocks.Should().NotBeNull("сценарий DbmsLocks обязан включать тег <dbmslocks/>");
        dbmslocks!.Parent!.Name.LocalName.Should().Be("config", "<dbmslocks/> — на уровне <config>");

        // Прочие сценарии тег НЕ эмитят.
        foreach (var scenario in Enum.GetValues<TechLogScenario>().Where(s => s != TechLogScenario.DbmsLocks))
        {
            BuildDoc(scenario).Descendants().Any(e => e.Name.LocalName == "dbmslocks")
                .Should().BeFalse($"сценарий {scenario}: <dbmslocks/> только для DbmsLocks");
        }
    }

    [Fact]
    public void Build_dbms_locks_isolates_tenant_inside_event()
    {
        // F-1 (MLC-246) распространяется и на DbmsLocks: при заданной ИБ p:processName лежит ВНУТРИ
        // <event> рядом с name. Поля lkX выводятся через <property name="all"/> (MLC-246).
        var doc = BuildDoc(TechLogScenario.DbmsLocks, infobase: "ut11_saratov");

        var ev = doc.Descendants().Single(e => e.Name.LocalName == "event");
        var props = ev.Elements().Where(c => c.Name.LocalName == "eq")
            .Select(c => c.Attribute("property")!.Value).ToArray();
        props.Should().Contain("name");
        props.Should().Contain("p:processName");

        doc.Descendants().Single(e => e.Name.LocalName == "log")
            .Elements().Any(e => e.Name.LocalName == "property" && e.Attribute("name")?.Value == "all")
            .Should().BeTrue("<property name=\"all\"/> выводит поля lkX (MLC-246)");
    }

    [Fact]
    public void Build_embeds_management_marker_recognized_by_IsManaged()
    {
        var xml = _builder.Build(TechLogScenario.Locks, null, Location, 2);
        xml.Should().Contain(LogcfgBuilder.Marker);
        _builder.IsManaged(xml).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<config><log location=\"x\"/></config>")]
    public void IsManaged_false_for_foreign_or_empty_config(string? content)
    {
        _builder.IsManaged(content).Should().BeFalse();
    }

    [Fact]
    public void Build_requires_collection_location()
    {
        var act = () => _builder.Build(TechLogScenario.Locks, null, "  ", 2);
        act.Should().Throw<ArgumentException>();
    }
}
