using System.Text;
using FluentAssertions;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Infrastructure.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-232 (этап B): парсер NDJSON-ТЖ 1С 8.5. Тесты кодируют ЗАКОН по фактам стенда MLC-229
// (40_TECHLOG §4/§7): NDJSON (объект на строку), BOM снимается, все значения строки, дубли ключей
// сохраняются целиком, duration нормализуется µs→секунды, парсер never-throws. Фикстуры — реальные
// структуры полей со стенда (§4/§8), значения обезличены.
public sealed class TechLogParserTests
{
    private readonly TechLogParser _parser = new();

    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "TechLog", "Fixtures", name);

    private static string[] ReadFixtureLines(string name)
        => File.ReadAllLines(FixturePath(name), Encoding.UTF8);

    // (i) Реальный DBMSSQL-объект из 40_TECHLOG §4 разбирается; ключевые поля доступны.
    [Fact]
    public void Parses_real_dbmssql_event_from_stand_sample()
    {
        const string line = "{\"ts\":\"2026-06-20T03:04:58.858004\",\"duration\":\"1\",\"name\":\"DBMSSQL\","
            + "\"depth\":\"5\",\"level\":\"DEBUG\",\"process\":\"rphost\",\"p:processName\":\"infobase01\","
            + "\"OSThread\":\"48908\",\"t:clientID\":\"17\",\"SessionID\":\"1\",\"Usr\":\"User\","
            + "\"DBMS\":\"DBMSSQL\",\"DataBase\":\"localhost\\\\infobase01\",\"Trans\":\"1\",\"dbpid\":\"65\","
            + "\"Sql\":\"SELECT 1\",\"Rows\":\"1\",\"Context\":\"Document.Posting\"}";

        var ev = _parser.ParseLine(line);

        ev.Should().NotBeNull();
        ev!.Name.Should().Be("DBMSSQL");
        ev.First("p:processName").Should().Be("infobase01");
        ev.First("Sql").Should().Be("SELECT 1");
        ev.First("Context").Should().Be("Document.Posting");
        ev.Ts.Should().Be("2026-06-20T03:04:58.858004");
        ev.Process.Should().Be("rphost");
        ev.Depth.Should().Be("5");
        ev.Level.Should().Be("DEBUG");
    }

    // (c) Все значения читаются как строки (даже числовые "Rows":"1").
    [Fact]
    public void Reads_all_values_as_strings()
    {
        var ev = _parser.ParseLine("{\"name\":\"DBMSSQL\",\"Rows\":\"1\",\"duration\":\"42\"}");

        ev.Should().NotBeNull();
        ev!.First("Rows").Should().Be("1");
        ev.First("duration").Should().Be("42");
        ev.RawDuration.Should().Be("42");
    }

    // (a) BOM в начале файла снимается, первое событие парсится.
    [Fact]
    public void Strips_bom_and_parses_first_event()
    {
        var rawBytes = File.ReadAllBytes(FixturePath("mixed-with-bom.ndjson"));
        rawBytes.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF }, "фикстура начинается с UTF-8 BOM");

        // Строка с BOM в начале — парсер обязан снять его и распарсить событие.
        var lineWithBom = "﻿{\"ts\":\"2026-06-20T03:04:58.858004\",\"name\":\"DBMSSQL\"}";
        var first = _parser.ParseLine(lineWithBom);

        first.Should().NotBeNull();
        first!.Name.Should().Be("DBMSSQL");
        first.First("ts").Should().Be("2026-06-20T03:04:58.858004");
    }

    // (b) NDJSON — несколько объектов на строках → несколько событий.
    [Fact]
    public void Parses_multiple_ndjson_lines_into_multiple_events()
    {
        var lines = ReadFixtureLines("dbmssql.ndjson");

        var result = _parser.ParseLines(lines);
        var events = result.Events.ToList();

        events.Should().HaveCount(2);
        events.Should().OnlyContain(e => e.Name == "DBMSSQL");
        result.ProcessedLines.Should().Be(2);
        result.SkippedLines.Should().Be(0);
    }

    // (d) Дубль ключа: t:clientID дважды — оба значения доступны, ничего не потеряно.
    [Fact]
    public void Preserves_duplicate_t_clientid_keys()
    {
        var line = ReadFixtureLines("mixed-with-bom.ndjson")[0];

        var ev = _parser.ParseLine(line);

        ev.Should().NotBeNull();
        ev!.All("t:clientID").Should().Equal("17", "19");
        ev.First("t:clientID").Should().Be("17");
        ev.Last("t:clientID").Should().Be("19");
    }

    // (d) Дубль ключа: Func дважды у завершения транзакции SDBL — оба значения доступны.
    [Fact]
    public void Preserves_duplicate_func_keys_on_sdbl_transaction()
    {
        var sdblWithDoubleFunc = ReadFixtureLines("sdbl.ndjson")[1];

        var ev = _parser.ParseLine(sdblWithDoubleFunc);

        ev.Should().NotBeNull();
        ev!.Name.Should().Be("SDBL");
        ev.All("Func").Should().Equal("BeginTransaction", "CommitTransaction");
        ev.First("Func").Should().Be("BeginTransaction");
        ev.Last("Func").Should().Be("CommitTransaction");
    }

    // (e) duration нормализуется µs→секунды.
    [Theory]
    [InlineData("60005971", 60005971L, 60.005971)]
    [InlineData("1", 1L, 0.000001)]
    [InlineData("21702949", 21702949L, 21.702949)]
    public void Normalizes_duration_microseconds_to_seconds(string raw, long expectedMicros, double expectedSeconds)
    {
        var ev = _parser.ParseLine($"{{\"name\":\"CALL\",\"duration\":\"{raw}\"}}");

        ev.Should().NotBeNull();
        ev!.RawDuration.Should().Be(raw);
        ev.DurationMicroseconds.Should().Be(expectedMicros);
        ev.DurationSeconds.Should().BeApproximately(expectedSeconds, 1e-9);
    }

    // (f) duration отсутствует/пустой/нечисловой → нормализованные поля null, не падает.
    [Theory]
    [InlineData("{\"name\":\"CALL\"}")]
    [InlineData("{\"name\":\"CALL\",\"duration\":\"\"}")]
    [InlineData("{\"name\":\"CALL\",\"duration\":\"abc\"}")]
    public void Tolerates_missing_empty_or_nonnumeric_duration(string line)
    {
        var ev = _parser.ParseLine(line);

        ev.Should().NotBeNull();
        ev!.DurationMicroseconds.Should().BeNull();
        ev.DurationSeconds.Should().BeNull();
    }

    // (g) «Поле-призрак»: нет Sql у DBMSSQL и нет p:processName у CALL → аксессор null, не падает.
    [Fact]
    public void Ghost_fields_return_null_without_throwing()
    {
        var dbmssqlNoSql = ReadFixtureLines("dbmssql.ndjson")[1];
        var evNoSql = _parser.ParseLine(dbmssqlNoSql);
        evNoSql.Should().NotBeNull();
        evNoSql!.First("Sql").Should().BeNull();
        evNoSql.Has("Sql").Should().BeFalse();

        var callNoProcessName = ReadFixtureLines("call.ndjson")[0];
        var evCall = _parser.ParseLine(callNoProcessName);
        evCall.Should().NotBeNull();
        evCall!.First("p:processName").Should().BeNull();
        evCall.All("p:processName").Should().BeEmpty();
    }

    // (h) Битая JSON-строка → пропущена, остальные события распарсены, парсер не бросил.
    [Fact]
    public void Skips_broken_lines_and_counts_them()
    {
        var lines = ReadFixtureLines("mixed-with-bom.ndjson");

        var result = _parser.ParseLines(lines);
        var events = result.Events.ToList();

        // 3 валидных события (DBMSSQL+BOM, CALL, EXCP), пропущены пустая строка и битый JSON.
        events.Should().HaveCount(3);
        events.Select(e => e.Name).Should().Equal("DBMSSQL", "CALL", "EXCP");
        result.ProcessedLines.Should().Be(3);
        result.SkippedLines.Should().Be(2);
    }

    [Fact]
    public void Broken_json_line_returns_null_and_does_not_throw()
    {
        var act = () => _parser.ParseLine("{this is not valid json,,,}");
        act.Should().NotThrow();
        _parser.ParseLine("{this is not valid json,,,}").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\uFEFF")]
    public void Empty_or_blank_lines_return_null(string? line)
    {
        _parser.ParseLine(line).Should().BeNull();
    }

    [Fact]
    public void Non_object_root_returns_null()
    {
        _parser.ParseLine("[1,2,3]").Should().BeNull();
        _parser.ParseLine("\"scalar\"").Should().BeNull();
    }

    // Потоковый разбор из TextReader (как StreamReader поверх файла на этапе C).
    [Fact]
    public void ParseReader_streams_events_from_text_reader()
    {
        using var reader = new StringReader(
            "{\"name\":\"EXCP\",\"Descr\":\"err\"}\n{\"name\":\"TLOCK\"}\n");

        var result = _parser.ParseReader(reader);
        var events = result.Events.ToList();

        events.Select(e => e.Name).Should().Equal("EXCP", "TLOCK");
        result.SkippedLines.Should().Be(0);
    }

    // TLOCK из фикстуры: поля дерева управляемых блокировок доступны (этап B — анализатор блокировок).
    [Fact]
    public void Parses_tlock_lock_tree_fields()
    {
        var ev = _parser.ParseLine(ReadFixtureLines("tlock.ndjson")[0]);

        ev.Should().NotBeNull();
        ev!.Name.Should().Be("TLOCK");
        ev.First("Regions").Should().Be("AccumulationRegister.Stock");
        ev.First("Locks").Should().Be("Exclusive");
    }

    // EXCP из фикстуры: тип исключения + текст доступны (этап B — анализатор исключений).
    [Fact]
    public void Parses_excp_exception_fields()
    {
        var ev = _parser.ParseLine(ReadFixtureLines("excp.ndjson")[0]);

        ev.Should().NotBeNull();
        ev!.Name.Should().Be("EXCP");
        ev.First("Exception").Should().Be("DataBaseException");
        ev.First("Descr").Should().Be("Lock request timeout exceeded");
    }

    // Счётчики актуальны только после полного перечисления ленивой последовательности.
    [Fact]
    public void Counters_are_zero_before_enumeration_and_final_after()
    {
        var result = _parser.ParseLines(ReadFixtureLines("dbmssql.ndjson"));

        result.ProcessedLines.Should().Be(0, "до перечисления счётчик ещё не заполнен");

        _ = result.Events.ToList();

        result.ProcessedLines.Should().Be(2);
    }
}
