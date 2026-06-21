using System.Xml.Linq;
using FluentAssertions;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Infrastructure.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-230 (ADR-57/58): генератор целевого logcfg.xml — ядро задачи, чистый C#. Тесты кодируют ЗАКОН
// по фактам стенда MLC-229 (40_TECHLOG §6/§8): format="json", НИКАКОГО фильтра длительности
// (Dur не работает для JSON-ТЖ 8.5), изоляция арендатора через точный <eq p:processName>, маркер.
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
        // 🛑 Фильтр длительности (<ge property="Dur"/>) НЕ работает для JSON-ТЖ 8.5 (ловит 0) —
        // объём режется ТОЛЬКО типом события и p:processName. Порог длительности — в парсере (этап B).
        foreach (var scenario in Enum.GetValues<TechLogScenario>())
        {
            var xml = _builder.Build(scenario, "mitpro", Location, 2);
            xml.Should().NotContain("<ge", $"сценарий {scenario}: запрещён фильтр <ge> по длительности");
            xml.Should().NotContain("Dur", $"сценарий {scenario}: запрещено свойство Dur");
            xml.Should().NotContain("duration", $"сценарий {scenario}: запрещён фильтр duration");
        }
    }

    [Fact]
    public void Build_emits_exact_processName_eq_when_infobase_given()
    {
        // Инвариант изоляции арендатора (60_SAFETY №2): точный <eq property="p:processName" value=...>.
        var doc = BuildDoc(infobase: "mitpro");
        var eq = doc.Descendants()
            .Where(e => e.Name.LocalName == "eq")
            .SingleOrDefault(e => e.Attribute("property")?.Value == "p:processName");

        eq.Should().NotBeNull("при заданной ИБ шаблон обязан содержать фильтр p:processName");
        eq!.Attribute("value")!.Value.Should().Be("mitpro");
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
    public void Build_never_emits_all_property()
    {
        // «Целевой, не полный» сбор (60_SAFETY №1): никогда <property name="all"/>.
        foreach (var scenario in Enum.GetValues<TechLogScenario>())
        {
            _builder.Build(scenario, null, Location, 2)
                .Should().NotContain("\"all\"", $"сценарий {scenario}: запрещён полный ТЖ");
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
        // Регресс MLC-245: <plansql>/<dump> не должны лежать ВНУТРИ <log> ни в одном сценарии.
        foreach (var scenario in Enum.GetValues<TechLogScenario>())
        {
            var doc = BuildDoc(scenario);
            var log = doc.Descendants().Single(e => e.Name.LocalName == "log");
            log.Descendants().Any(e => e.Name.LocalName is "plansql" or "dump")
                .Should().BeFalse($"сценарий {scenario}: теги-обогатители не внутри <log>");
        }
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

    // MLC-236: DbmsLocks эмитит событие DBMSSQL.
    [Fact]
    public void Build_dbmslocks_emits_dbmssql_event()
    {
        var doc = BuildDoc(TechLogScenario.DbmsLocks);
        var events = doc.Descendants()
            .Where(e => e.Name.LocalName == "event")
            .SelectMany(e => e.Descendants().Where(c => c.Name.LocalName == "eq"))
            .Where(eq => eq.Attribute("property")?.Value == "name")
            .Select(eq => eq.Attribute("value")!.Value)
            .ToArray();

        events.Should().BeEquivalentTo(["DBMSSQL"], "DbmsLocks собирает только DBMSSQL");
    }

    // MLC-236: тег <dbmslocks/> должен быть на уровне <config> (не внутри <log>).
    [Fact]
    public void Build_dbmslocks_emits_dbmslocks_tag_at_config_level()
    {
        var dbmslocks = BuildDoc(TechLogScenario.DbmsLocks)
            .Descendants().SingleOrDefault(e => e.Name.LocalName == "dbmslocks");

        dbmslocks.Should().NotBeNull("<dbmslocks/> обязан быть в конфиге для сценария DbmsLocks");
        dbmslocks!.Parent!.Name.LocalName.Should().Be("config",
            "<dbmslocks/> обязан быть на уровне <config>, а не внутри <log>");
    }

    // MLC-236: свойства lka/lkp/lkpid/lkaid/lksrc/lkpto/lkato присутствуют как
    // <property name=...> ВНУТРИ <log> (целевой сбор, не <property name="all"/>).
    [Fact]
    public void Build_dbmslocks_includes_lkx_properties_inside_log()
    {
        var doc = BuildDoc(TechLogScenario.DbmsLocks);
        var log = doc.Descendants().Single(e => e.Name.LocalName == "log");
        var propNames = log.Descendants()
            .Where(e => e.Name.LocalName == "property")
            .Select(e => e.Attribute("name")?.Value)
            .Where(v => v != null)
            .ToArray();

        string[] expected = ["lka", "lkp", "lkpid", "lkaid", "lksrc", "lkpto", "lkato"];
        propNames.Should().Contain(expected, "все целевые lkX-свойства должны быть в <log>");
    }

    // MLC-236: при заданной ИБ в DbmsLocks присутствует <eq p:processName>.
    [Fact]
    public void Build_dbmslocks_with_infobase_includes_process_name_filter()
    {
        var doc = BuildDoc(TechLogScenario.DbmsLocks, infobase: "mitpro");
        var eq = doc.Descendants()
            .Where(e => e.Name.LocalName == "eq")
            .SingleOrDefault(e => e.Attribute("property")?.Value == "p:processName");

        eq.Should().NotBeNull("при заданной ИБ обязан быть фильтр p:processName");
        eq!.Attribute("value")!.Value.Should().Be("mitpro");
    }

    // MLC-236: DbmsLocks НЕ эмитит <property name="all"/> — целевой, не полный сбор (60_SAFETY №1).
    [Fact]
    public void Build_dbmslocks_never_emits_all_property()
    {
        var xml = _builder.Build(TechLogScenario.DbmsLocks, null, Location, 2);

        xml.Should().NotContain("\"all\"", "DbmsLocks: запрещён полный ТЖ (<property name=\"all\"/>)");
    }

    // MLC-236: <dbmslocks/> НЕ вложен внутрь <log> (регресс MLC-245 — обогатители не внутри <log>).
    [Fact]
    public void Build_dbmslocks_tag_is_not_nested_inside_log()
    {
        var doc = BuildDoc(TechLogScenario.DbmsLocks);
        var log = doc.Descendants().Single(e => e.Name.LocalName == "log");

        log.Descendants().Any(e => e.Name.LocalName == "dbmslocks")
            .Should().BeFalse("<dbmslocks/> не должен быть вложен внутрь <log>");
    }

    // Для не-DbmsLocks сценариев тег <dbmslocks/> не эмитится.
    [Theory]
    [InlineData(TechLogScenario.Locks)]
    [InlineData(TechLogScenario.SlowQueries)]
    [InlineData(TechLogScenario.Exceptions)]
    [InlineData(TechLogScenario.GeneralSlow)]
    public void Build_non_dbmslocks_scenarios_do_not_emit_dbmslocks_tag(TechLogScenario scenario)
    {
        var doc = BuildDoc(scenario);
        doc.Descendants().Any(e => e.Name.LocalName == "dbmslocks")
            .Should().BeFalse($"сценарий {scenario}: тег <dbmslocks/> не должен появляться");
    }
}
