using System.Text;
using FluentAssertions;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Infrastructure.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-234 (этап B): анализатор долгих запросов к СУБД из событий DBMSSQL.
// ГРАНИЦА: ТОЛЬКО DBMSSQL. Все остальные события игнорируются.
// Тесты кодируют семантику 40_TECHLOG §4/§6/§7/§8:
//   • порог длительности — в анализаторе, фильтр Dur в logcfg для JSON-ТЖ 8.5 не работает;
//   • «поля-призраки»: Sql у DBMSSQL иногда отсутствует (принцип never-throws);
//   • привязка к ИБ через p:processName с GUID-суффиксом фоновых сессий;
//   • группировка по нормализованному SQL (литералы → «?»).
// Фикстуры: dbmssql.ndjson — тяжёлый (21702949 µs) + лёгкий (1 µs, без Sql);
//            dbmssql-slow.ndjson — 5 записей (три SELECT c _Code 100/999/777 → одна группа,
//            UPDATE с GUID p:processName, один SELECT ниже дефолтного порога 1 000 000 µs).
public sealed class SlowQueryAnalyzerTests
{
    private readonly TechLogParser _parser = new();
    private readonly SlowQueryAnalyzer _analyzer = new();

    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "TechLog", "Fixtures", name);

    private static string[] ReadFixtureLines(string name)
        => File.ReadAllLines(FixturePath(name), Encoding.UTF8);

    private IEnumerable<TechLogEvent> ParseFixture(string name)
        => _parser.ParseLines(ReadFixtureLines(name)).Events;

    // (a) Из dbmssql.ndjson тяжёлый запрос (21702949 µs ≈ 21.7 с) попадает в TopQueries
    //     при пороге 1 000 000 µs; лёгкий (1 µs) отсекается.
    [Fact]
    public void Heavy_query_above_threshold_appears_in_top_queries()
    {
        var events = ParseFixture("dbmssql.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000);

        result.TotalDbmssqlEvents.Should().Be(2, "два DBMSSQL в файле");
        result.EventsAboveThreshold.Should().Be(1, "только тяжёлый (21702949 µs) ≥ порога");
        result.TopQueries.Should().HaveCount(1);
        result.TopQueries[0].DurationMicroseconds.Should().Be(21_702_949);
    }

    // (b) TopQueries отсортированы по длительности убывающим.
    [Fact]
    public void Top_queries_are_sorted_by_duration_descending()
    {
        var events = ParseFixture("dbmssql-slow.ndjson");

        // порог 1_000_000 µs — пройдут записи с 5123456/7654321/2000000/3500000 µs
        // (500000 µs = запись #4 — ниже порога)
        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000);

        result.TopQueries.Should().HaveCountGreaterThan(1);
        for (var i = 1; i < result.TopQueries.Count; i++)
        {
            result.TopQueries[i].DurationMicroseconds.Should()
                .BeLessOrEqualTo(result.TopQueries[i - 1].DurationMicroseconds,
                    "TopQueries должны идти по убыванию длительности");
        }

        result.TopQueries[0].DurationMicroseconds.Should().Be(7_654_321,
            "самый долгий — 7654321 µs (второй SELECT с _Code = 999)");
    }

    // (c) Поля Sql, Context, DbPid, Rows читаются из полной записи dbmssql.ndjson.
    //     Первая строка dbmssql.ndjson: duration=1 µs (ниже порога для теста), возьмём
    //     порог 0, чтобы обе записи прошли, и проверим поля первой.
    [Fact]
    public void Full_record_fields_sql_context_dbpid_rows_are_captured()
    {
        var lines = ReadFixtureLines("dbmssql.ndjson");
        // Берём первую строку отдельно (duration=1 µs, полная), порог = 0
        var ev = _parser.ParseLine(lines[0]);
        ev.Should().NotBeNull();

        var result = _analyzer.Analyze([ev!], thresholdMicroseconds: 0);

        result.TopQueries.Should().HaveCount(1);
        var entry = result.TopQueries[0];
        entry.Sql.Should().Be("SELECT T1._Field FROM _Table T1 WHERE T1._Ref = @P1");
        entry.Context.Should().Contain("Document.Posting");
        entry.DbPid.Should().Be("65");
        entry.Rows.Should().Be("1");
    }

    // (d) Запись без Sql (вторая строка dbmssql.ndjson) не валит анализ; при достаточной
    //     длительности попадает в TopQueries с Sql=null; в SimilarGroups не входит.
    [Fact]
    public void Record_without_sql_appears_in_top_queries_with_null_sql_not_in_groups()
    {
        var lines = ReadFixtureLines("dbmssql.ndjson");
        // Вторая запись: duration=21702949 µs, нет поля Sql
        var ev = _parser.ParseLine(lines[1]);
        ev.Should().NotBeNull();

        var result = _analyzer.Analyze([ev!], thresholdMicroseconds: 1_000_000);

        result.TopQueries.Should().HaveCount(1, "одна запись прошла порог");
        result.TopQueries[0].Sql.Should().BeNull("поле Sql у второй записи отсутствует");
        result.SimilarGroups.Should().BeEmpty("запись без Sql в группировку не входит");
        result.SkippedEvents.Should().Be(0, "отсутствие Sql — не ошибка, never-throws");
    }

    // (e) Из dbmssql-slow.ndjson: три SELECT с _Code=100/999/777 схлопываются в одну группу.
    //     UPDATE и отдельный SELECT — отдельные группы.
    //     MLC-248: агрегат SimilarGroups НЕЗАВИСИМ от порога — под-пороговый SELECT T2 (500000 µs)
    //     теперь ВХОДИТ в группы (но НЕ в TopQueries). Группы по ВСЕМ DBMSSQL с непустым Sql.
    [Fact]
    public void Similar_selects_with_different_literals_form_single_group()
    {
        var events = ParseFixture("dbmssql-slow.ndjson");

        // Порог 1 000 000 µs: в TopQueries пройдут 5123456/7654321/2000000/3500000 µs (4 шт),
        // не пройдёт только 500000 µs (#4 — SELECT T2). Но в SimilarGroups — ВСЕ 5 событий.
        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000);

        // Три SELECT с _Code=100/999/777 нормализуются одинаково → одна группа с Count=3
        var selectGroup = result.SimilarGroups
            .FirstOrDefault(g => g.NormalizedSql.Contains("_TableX", StringComparison.Ordinal));
        selectGroup.Should().NotBeNull("три SELECT с разными _Code должны быть в одной группе");
        selectGroup!.Count.Should().Be(3);
        selectGroup.NormalizedSql.Should().Contain("_TableX", "нормализованный SQL узнаём по имени таблицы");
        selectGroup.TotalDurationMicroseconds.Should().Be(5_123_456 + 7_654_321 + 3_500_000,
            "суммарная длительность трёх SELECT");
        selectGroup.MaxDurationMicroseconds.Should().Be(7_654_321);

        // UPDATE — отдельная группа
        var updateGroup = result.SimilarGroups
            .FirstOrDefault(g => g.NormalizedSql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase));
        updateGroup.Should().NotBeNull("UPDATE — отдельная группа");
        updateGroup!.Count.Should().Be(1);

        // MLC-248: под-пороговый SELECT _TableZ (500000 µs) — отдельная группа, в TopQueries его нет.
        var underThresholdGroup = result.SimilarGroups
            .FirstOrDefault(g => g.NormalizedSql.Contains("_TableZ", StringComparison.Ordinal));
        underThresholdGroup.Should().NotBeNull("под-пороговый запрос всё равно учитывается в агрегате");
        underThresholdGroup!.Count.Should().Be(1);
        underThresholdGroup.TotalDurationMicroseconds.Should().Be(500_000);
        result.TopQueries.Should().NotContain(q => q.Sql != null && q.Sql.Contains("_TableZ", StringComparison.Ordinal),
            "под-пороговый запрос не попадает в топ единичных долгих");
    }

    // (e2) MLC-248 — ЯДРО: «много быстрых запросов одной формы» → группа есть, TopQueries пуст
    //      при высоком пороге. Это кейс «закрытия месяца» (стенд-приёмка 1.2): тысячи мелких в сумме.
    [Fact]
    public void Many_fast_queries_form_group_even_when_top_is_empty_at_high_threshold()
    {
        // 1000 быстрых запросов одной формы по 50 000 µs (0.05 c) — ни один не дотягивает до порога 1 c.
        var lines = Enumerable.Range(0, 1000).Select(i =>
            "{\"ts\":\"2026-06-20T05:00:00.000000\",\"duration\":\"50000\",\"name\":\"DBMSSQL\"," +
            "\"process\":\"rphost\",\"p:processName\":\"infobase01\"," +
            $"\"Sql\":\"SELECT T1._Field FROM _Hot T1 WHERE T1._Ref = @P1 AND T1._Code = {i}\"}}");
        var events = _parser.ParseLines(lines).Events;

        // Порог 1 000 000 µs (1 c): НИ ОДИН запрос не проходит в TopQueries.
        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000);

        result.TopQueries.Should().BeEmpty("ни один запрос не длиннее порога 1 c");
        result.EventsAboveThreshold.Should().Be(0);
        result.TotalDbmssqlEvents.Should().Be(1000);

        // Но агрегат «похожие» ВСПЛЫВАЕТ: одна группа со всеми 1000 вхождениями.
        result.SimilarGroups.Should().ContainSingle("разные _Code нормализуются в одну форму");
        var group = result.SimilarGroups[0];
        group.Count.Should().Be(1000);
        group.TotalDurationMicroseconds.Should().Be(1000L * 50_000, "1000 × 50 000 µs = суммарно 50 c");
        group.MaxDurationMicroseconds.Should().Be(50_000);
        group.NormalizedSql.Should().Contain("_Hot");
    }

    // (f) Привязка к ИБ: запись с p:processName="infobase01_<GUID>" → InfobaseName="infobase01",
    //     RawProcessName содержит GUID-суффикс.
    [Fact]
    public void Infobase_name_normalized_from_guid_suffixed_process_name()
    {
        var events = ParseFixture("dbmssql-slow.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000);

        // Третья запись dbmssql-slow.ndjson: p:processName = "infobase01_296ee6d8-..."
        var entry = result.TopQueries
            .FirstOrDefault(e => e.RawProcessName?.Contains("296ee6d8") == true);
        entry.Should().NotBeNull("запись с GUID-суффиксом должна присутствовать в топе");
        entry!.InfobaseName.Should().Be("infobase01",
            "GUID-суффикс фоновой сессии должен быть отсечён");
        entry.RawProcessName.Should().Contain("296ee6d8",
            "сырое значение p:processName сохраняется без изменений");
    }

    // (g) Параметры thresholdMicroseconds и topN уважаются анализатором.
    //     MLC-248: высокий порог опустошает TopQueries, но SimilarGroups (независимый агрегат) — нет:
    //     первая запись dbmssql.ndjson имеет Sql (1 µs) → попадает в группу даже под порогом.
    [Fact]
    public void High_threshold_yields_empty_top_but_groups_still_aggregate_sql_events()
    {
        var events = ParseFixture("dbmssql.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: long.MaxValue);

        result.TopQueries.Should().BeEmpty("порог выше любой длительности в файле");
        result.TotalDbmssqlEvents.Should().Be(2);
        result.EventsAboveThreshold.Should().Be(0);
        // Первая запись имеет Sql (1 µs) → агрегат «похожие» учитывает её независимо от порога (MLC-248).
        result.SimilarGroups.Should().ContainSingle("событие с Sql попадает в группу под любым порогом");
        result.SimilarGroups[0].Count.Should().Be(1);
        result.SimilarGroups[0].TotalDurationMicroseconds.Should().Be(1);
    }

    [Fact]
    public void TopN_limits_result_count()
    {
        var events = ParseFixture("dbmssql-slow.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000, topN: 1);

        result.TopQueries.Should().HaveCount(1, "topN=1 → ровно одна запись в TopQueries");
        result.TopQueries[0].DurationMicroseconds.Should().Be(7_654_321,
            "в топ-1 должна попасть самая долгая запись");
    }

    // (h) Не-DBMSSQL события игнорируются: поток tlock.ndjson/sdbl.ndjson →
    //     TotalDbmssqlEvents==0, пустые топ/группы, без исключений.
    [Fact]
    public void Non_dbmssql_events_are_ignored_completely()
    {
        var tlockLines = ReadFixtureLines("tlock.ndjson");
        var sdblLines = ReadFixtureLines("sdbl.ndjson");
        var allLines = tlockLines.Concat(sdblLines);
        var events = _parser.ParseLines(allLines).Events;

        var result = _analyzer.Analyze(events);

        result.TotalDbmssqlEvents.Should().Be(0, "TLOCK и SDBL — не DBMSSQL");
        result.TopQueries.Should().BeEmpty();
        result.SimilarGroups.Should().BeEmpty();
        result.SkippedEvents.Should().Be(0);
    }

    // (i) Устойчивость: пустой поток не бросает и возвращает пустой результат.
    [Fact]
    public void Empty_event_stream_returns_empty_result()
    {
        var result = _analyzer.Analyze([]);

        result.TopQueries.Should().BeEmpty();
        result.SimilarGroups.Should().BeEmpty();
        result.TotalDbmssqlEvents.Should().Be(0);
        result.EventsAboveThreshold.Should().Be(0);
        result.SkippedEvents.Should().Be(0);
    }

    // (i) Устойчивость: смешанный поток с BOM (mixed-with-bom.ndjson) не бросает.
    [Fact]
    public void Mixed_stream_with_bom_does_not_throw()
    {
        var events = ParseFixture("mixed-with-bom.ndjson");

        var act = () => _analyzer.Analyze(events);
        act.Should().NotThrow("анализатор never-throws на любом входе");
    }

    // (j) NormalizeSql: два SQL с разными литералами/числами/параметрами дают один ключ.
    [Theory]
    [InlineData(
        "SELECT T1._Field1 FROM _TableX T1 WHERE T1._Ref = @P1 AND T1._Code = 100",
        "SELECT T1._Field1 FROM _TableX T1 WHERE T1._Ref = @P1 AND T1._Code = 999")]
    [InlineData(
        "SELECT * FROM T WHERE Id = 1 AND Name = 'Alice'",
        "SELECT * FROM T WHERE Id = 42 AND Name = 'Bob'")]
    [InlineData(
        "UPDATE Tbl SET Val = @Param1 WHERE Ref = @Param2",
        "UPDATE Tbl SET Val = @OtherParam WHERE Ref = @YetAnother")]
    public void NormalizeSql_different_literals_produce_same_key(string sqlA, string sqlB)
    {
        var normA = SlowQueryAnalyzer.NormalizeSql(sqlA);
        var normB = SlowQueryAnalyzer.NormalizeSql(sqlB);

        normA.Should().Be(normB,
            "SQL, отличающиеся только значениями литералов/параметров, должны давать одинаковый ключ");
    }

    // (j) NormalizeSql: лишние пробелы схлопываются в один.
    [Fact]
    public void NormalizeSql_collapses_whitespace()
    {
        var result = SlowQueryAnalyzer.NormalizeSql("SELECT  T1._Field  FROM  _Table  T1");

        result.Should().NotContain("  ", "лишние пробелы должны быть схлопнуты в один");
        result.Should().Be(result.Trim(), "результат не должен иметь ведущих/хвостовых пробелов");
    }

    // (k) MLC-247 A1: поле плана называется planSQLText (офиц. спека §7, строчная p, SQL заглавными).
    //     TechLogEvent.First сравнивает ключи через StringComparison.Ordinal — при неверном регистре
    //     (прежнее «PlanSQLText») план НИКОГДА не прочитался бы (тихий null). Здесь plansql задан в JSON →
    //     анализатор кладёт его в PlanText.
    [Fact]
    public void Plan_field_planSQLText_is_captured_into_plan_text()
    {
        const string line =
            "{\"ts\":\"2026-06-20T05:00:00.000000\",\"duration\":\"2000000\",\"name\":\"DBMSSQL\"," +
            "\"process\":\"rphost\",\"p:processName\":\"infobase01\",\"DataBase\":\"localhost\\\\infobase01\"," +
            "\"Sql\":\"SELECT T1._Field FROM _Table T1\"," +
            "\"planSQLText\":\"Rows,Executes,StmtText\\n5,1,SELECT ...\"}";
        var ev = _parser.ParseLine(line);
        ev.Should().NotBeNull();

        var result = _analyzer.Analyze([ev!], thresholdMicroseconds: 1_000_000);

        result.TopQueries.Should().HaveCount(1);
        result.TopQueries[0].PlanText.Should().NotBeNull("поле planSQLText (спека §7) должно читаться");
        result.TopQueries[0].PlanText.Should().Contain("StmtText");
    }

    // (k) MLC-247 A1: отсутствие поля плана — не ошибка (best-effort): PlanText = null, без падения.
    [Fact]
    public void Missing_plan_field_yields_null_plan_text_without_throwing()
    {
        var lines = ReadFixtureLines("dbmssql.ndjson");
        var ev = _parser.ParseLine(lines[0]); // полная запись, но без planSQLText
        ev.Should().NotBeNull();

        var result = _analyzer.Analyze([ev!], thresholdMicroseconds: 0);

        result.TopQueries.Should().HaveCount(1);
        result.TopQueries[0].PlanText.Should().BeNull("планов нет в фикстуре — best-effort null, не падение");
    }

    // (j) NormalizeSql: строковые литералы схлопываются (в том числе содержащие цифры).
    [Fact]
    public void NormalizeSql_replaces_string_literals_before_numbers()
    {
        var normA = SlowQueryAnalyzer.NormalizeSql("SELECT * FROM T WHERE Name = '100ABC'");
        var normB = SlowQueryAnalyzer.NormalizeSql("SELECT * FROM T WHERE Name = 'XYZ'");

        normA.Should().Be(normB,
            "строковые литералы с разным содержимым (включая цифры) схлопываются одинаково");
    }
}
