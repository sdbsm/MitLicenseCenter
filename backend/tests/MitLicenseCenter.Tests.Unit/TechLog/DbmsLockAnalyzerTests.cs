using System.Text;
using FluentAssertions;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Infrastructure.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-236 (этап B): анализатор СУБД-блокировок (dbmslocks/lkX).
// ГРАНИЦА: ТОЛЬКО СУБД-уровень (DBMSSQL + поля lkX — отдельный механизм от управляемых блокировок
// 1С). Управляемые блокировки (TLOCK/TTIMEOUT/TDEADLOCK) — ILockTreeAnalyzer (MLC-233).
//
// ⚠ Структура полей lkX по документации (infostart 1431026, 40_TECHLOG §5);
//   точная форма в JSON-ТЖ 8.5 подлежит подтверждению на стенде (приёмка владельца).
//
// Фикстура dbmslocks.ndjson (4 события):
//   #1 — жертва (lkp=1): t:connectID=7, lksrc=9, lkpid=202, lkpto=5; p:processName="infobase01"
//   #2 — источник (lka=1): t:connectID=9, lkaid="202,203", lkato=8; p:processName="infobase01"
//   #3 — жертва (lkp=1): t:connectID=15, lksrc=99 (источника нет); p:processName с GUID-суффиксом
//   #4 — обычный DBMSSQL без lkX (игнор)
public sealed class DbmsLockAnalyzerTests
{
    private readonly TechLogParser _parser = new();
    private readonly DbmsLockAnalyzer _analyzer = new();

    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "TechLog", "Fixtures", name);

    private static string[] ReadFixtureLines(string name)
        => File.ReadAllLines(FixturePath(name), Encoding.UTF8);

    private IEnumerable<TechLogEvent> ParseFixture(string name)
        => _parser.ParseLines(ReadFixtureLines(name)).Events;

    // (a) Из dbmslocks.ndjson строится ребро: жертва connectID=7 / lksrc=9 → источник connectID=9,
    //     SourceMatched==true; SourceLkaid и SourceSql источника прочитаны.
    [Fact]
    public void Builds_wait_edge_from_victim_to_matched_source()
    {
        var events = ParseFixture("dbmslocks.ndjson");

        var result = _analyzer.Analyze(events);

        // Два ребра: одно с SourceMatched=true, одно с SourceMatched=false
        result.WaitEdges.Should().HaveCount(2, "две жертвы (lkp=1): одна с источником, другая без");

        var edge = result.WaitEdges.First(e => e.SourceMatched);
        edge.VictimConnectId.Should().Be("7");
        edge.VictimLksrc.Should().Be("9");
        edge.SourceConnectId.Should().Be("9");
        edge.SourceMatched.Should().BeTrue();
        edge.SourceLkaid.Should().Be("202,203", "lkaid источника читается");
        edge.SourceSql.Should().NotBeNullOrEmpty("Sql источника читается");
    }

    // (b) VictimLkpto и SourceLkato читаются из событий.
    [Fact]
    public void Reads_victim_lkpto_and_source_lkato()
    {
        var events = ParseFixture("dbmslocks.ndjson");

        var result = _analyzer.Analyze(events);

        var edge = result.WaitEdges.First(e => e.SourceMatched);
        edge.VictimLkpto.Should().Be("5", "lkpto жертвы (#1: lkpto=5)");
        edge.SourceLkato.Should().Be("8", "lkato источника (#2: lkato=8)");
    }

    // (c) Жертва с lksrc=99 (источника нет) → ребро с SourceMatched==false,
    //     UnmatchedVictimCount увеличен, SourceConnectId==null.
    [Fact]
    public void Unmatched_victim_produces_partial_edge_with_source_matched_false()
    {
        var events = ParseFixture("dbmslocks.ndjson");

        var result = _analyzer.Analyze(events);

        var partial = result.WaitEdges.FirstOrDefault(e => !e.SourceMatched);
        partial.Should().NotBeNull("жертва lksrc=99 не найдена в источниках — частичное ребро");
        partial!.SourceMatched.Should().BeFalse();
        partial.SourceConnectId.Should().BeNull("источник не найден → null");
        result.UnmatchedVictimCount.Should().Be(1, "одна жертва без источника");
    }

    // (d) Обычный DBMSSQL без lkX игнорируется: не попадает в рёбра, LkEventsProcessed не растёт.
    [Fact]
    public void Regular_dbmssql_without_lkx_is_ignored()
    {
        // #4 в dbmslocks.ndjson — обычный DBMSSQL без lka/lkp
        var lines = ReadFixtureLines("dbmslocks.ndjson");
        var ev = _parser.ParseLine(lines[3]); // 4-я строка, 0-based
        ev.Should().NotBeNull();

        var result = _analyzer.Analyze([ev!]);

        result.WaitEdges.Should().BeEmpty("DBMSSQL без lkX — не блокировка");
        result.LkEventsProcessed.Should().Be(0, "событие без lkX не считается");
    }

    // (e) Привязка к ИБ: жертва с p:processName="infobase01_<GUID>" → InfobaseName=="infobase01",
    //     RawProcessName содержит полное значение с GUID-суффиксом.
    [Fact]
    public void Infobase_name_normalized_from_guid_suffixed_process_name()
    {
        var events = ParseFixture("dbmslocks.ndjson");

        var result = _analyzer.Analyze(events);

        // #3: p:processName="infobase01_296ee6d8-1234-5678-abcd-ef0123456789"
        var edge = result.WaitEdges.FirstOrDefault(e => e.RawProcessName?.Contains("296ee6d8") == true);
        edge.Should().NotBeNull("жертва с GUID-суффиксом в p:processName должна присутствовать");
        edge!.InfobaseName.Should().Be("infobase01", "GUID-суффикс отсекается");
        edge.RawProcessName.Should().Contain("296ee6d8", "сырое значение p:processName сохраняется");
    }

    // (f) Не-DBMSSQL события (tlock.ndjson / excp.ndjson) игнорируются полностью:
    //     ни рёбер, ни LkEventsProcessed.
    [Fact]
    public void Non_dbmssql_events_are_completely_ignored()
    {
        var tlockLines = ReadFixtureLines("tlock.ndjson");
        var excpLines = ReadFixtureLines("excp.ndjson");
        var allLines = tlockLines.Concat(excpLines);
        var events = _parser.ParseLines(allLines).Events;

        var result = _analyzer.Analyze(events);

        result.WaitEdges.Should().BeEmpty("TLOCK и EXCP — не DBMSSQL");
        result.LkEventsProcessed.Should().Be(0);
        result.SkippedEvents.Should().Be(0);
    }

    // (g) Устойчивость: пустой поток не бросает, счётчики нулевые, рёбра пусты.
    [Fact]
    public void Empty_event_stream_returns_empty_result()
    {
        var result = _analyzer.Analyze([]);

        result.WaitEdges.Should().BeEmpty();
        result.LkEventsProcessed.Should().Be(0);
        result.UnmatchedVictimCount.Should().Be(0);
        result.SkippedEvents.Should().Be(0);
    }

    // (g) Устойчивость: смешанный поток с BOM (mixed-with-bom.ndjson) не бросает,
    //     счётчики консистентны (LkEventsProcessed ≤ кол-ву DBMSSQL в потоке).
    [Fact]
    public void Mixed_stream_with_bom_does_not_throw_and_counts_are_consistent()
    {
        var events = ParseFixture("mixed-with-bom.ndjson");

        var act = () => _analyzer.Analyze(events);
        act.Should().NotThrow("анализатор never-throws на любом входе");

        var result = _analyzer.Analyze(events);
        result.UnmatchedVictimCount.Should().BeLessOrEqualTo(result.WaitEdges.Count,
            "UnmatchedVictimCount не может превышать число рёбер (каждая жертва → ребро)");
        result.LkEventsProcessed.Should().BeGreaterOrEqualTo(0, "счётчик неотрицателен");
    }

    // LkEventsProcessed считает оба типа lkX-событий: жертвы (lkp=1) и источники (lka=1).
    [Fact]
    public void Lk_events_processed_counts_both_victims_and_sources()
    {
        var events = ParseFixture("dbmslocks.ndjson");

        var result = _analyzer.Analyze(events);

        // #1 — жертва (lkp=1), #2 — источник (lka=1), #3 — жертва (lkp=1); #4 — без lkX (игнор)
        result.LkEventsProcessed.Should().Be(3, "три события с lkX (#1 жертва, #2 источник, #3 жертва)");
    }

    // Поля VictimSql и VictimContext жертвы читаются.
    [Fact]
    public void Victim_sql_and_context_are_captured()
    {
        var events = ParseFixture("dbmslocks.ndjson");

        var result = _analyzer.Analyze(events);

        var edge = result.WaitEdges.First(e => e.SourceMatched);
        edge.VictimSql.Should().NotBeNullOrEmpty("Sql жертвы (#1) присутствует в фикстуре");
        edge.VictimContext.Should().NotBeNullOrEmpty("Context жертвы (#1) присутствует в фикстуре");
    }

    // Поле Database жертвы читается из поля DataBase события.
    [Fact]
    public void Victim_database_field_is_captured()
    {
        var events = ParseFixture("dbmslocks.ndjson");

        var result = _analyzer.Analyze(events);

        var edge = result.WaitEdges.First(e => e.SourceMatched);
        edge.Database.Should().Be(@"localhost\infobase01");
    }

    // VictimLkpid читается из события жертвы.
    [Fact]
    public void Victim_lkpid_is_captured()
    {
        var events = ParseFixture("dbmslocks.ndjson");

        var result = _analyzer.Analyze(events);

        var edge = result.WaitEdges.First(e => e.SourceMatched);
        edge.VictimLkpid.Should().Be("202", "lkpid жертвы (#1: lkpid=202)");
    }
}
