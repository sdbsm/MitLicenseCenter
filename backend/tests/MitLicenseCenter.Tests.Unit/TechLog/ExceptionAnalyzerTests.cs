using System.Text;
using FluentAssertions;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Infrastructure.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-235 (этап B): анализатор исключений платформы 1С из событий EXCP.
// ГРАНИЦА: ТОЛЬКО EXCP. Все остальные события игнорируются.
// Тесты кодируют семантику 40_TECHLOG §5/§7/§8:
//   • группировка по типу Exception + нормализованному Descr (числа → «#»);
//   • DataBaseException → флаг IsDatabaseException (дедлок СУБД = 2 EXCP, §7);
//   • устойчивость к «полям-призракам» (Descr/Context могут отсутствовать) — never-throws;
//   • привязка к ИБ через p:processName с GUID-суффиксом фоновых сессий;
//   • параметр topN уважается; не-EXCP события игнорируются.
// Фикстуры:
//   excp.ndjson         — два EXCP DataBaseException (пара дедлока, §7);
//   excp-mixed.ndjson   — смесь типов, нормализация Descr, GUID-суффикс, EXCP без Descr,
//                         один DBMSSQL (не-EXCP, должен игнорироваться).
public sealed class ExceptionAnalyzerTests
{
    private readonly TechLogParser _parser = new();
    private readonly ExceptionAnalyzer _analyzer = new();

    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "TechLog", "Fixtures", name);

    private static string[] ReadFixtureLines(string name)
        => File.ReadAllLines(FixturePath(name), Encoding.UTF8);

    private IEnumerable<TechLogEvent> ParseFixture(string name)
        => _parser.ParseLines(ReadFixtureLines(name)).Events;

    // (a) Из excp.ndjson: два EXCP DataBaseException «Lock request timeout exceeded»
    //     схлопываются в одну группу с Count=2 (§7: дедлок СУБД = пара EXCP).
    [Fact]
    public void Two_database_exceptions_merge_into_one_group_count_two()
    {
        var events = ParseFixture("excp.ndjson");

        var result = _analyzer.Analyze(events);

        result.TotalExcpEvents.Should().Be(2, "два EXCP в файле");
        result.TopExceptions.Should().HaveCount(1, "оба EXCP одного типа → одна группа");
        result.TopExceptions[0].Count.Should().Be(2, "группа содержит оба вхождения");
    }

    // (b) IsDatabaseException = true для группы с ExceptionType = DataBaseException.
    [Fact]
    public void Database_exception_group_has_is_database_exception_flag()
    {
        var events = ParseFixture("excp.ndjson");

        var result = _analyzer.Analyze(events);

        result.TopExceptions.Should().HaveCount(1);
        result.TopExceptions[0].IsDatabaseException.Should().BeTrue(
            "DataBaseException — кандидат на блокировки/дедлоки СУБД (40_TECHLOG §7)");
        result.TopExceptions[0].ExceptionType.Should().Be("DataBaseException");
        result.DatabaseExceptionEvents.Should().Be(2, "оба EXCP имеют тип DataBaseException");
    }

    // (c) Нормализация Descr: «timeout after 100 ms» и «timeout after 999 ms»
    //     схлопываются в одну группу (числа → «#»).
    [Fact]
    public void Similar_descr_with_different_numbers_merge_into_one_group()
    {
        var events = ParseFixture("excp-mixed.ndjson");

        var result = _analyzer.Analyze(events);

        // SystemException: две записи с «Lock request timeout exceeded after 100 ms»
        // и «… after 999 ms» — после нормализации одна группа Count=2.
        var sysGroup = result.TopExceptions
            .FirstOrDefault(g => string.Equals(
                g.ExceptionType, "SystemException", StringComparison.Ordinal));

        sysGroup.Should().NotBeNull("SystemException должна присутствовать в топе");
        sysGroup!.Count.Should().Be(2,
            "оба SystemException с разными числами схлопываются нормализацией в одну группу");
        sysGroup.NormalizedDescr.Should().Contain("#",
            "числа в Descr схлопнуты к плейсхолдеру «#»");
    }

    // (d) Разные типы Exception → разные группы; топ отсортирован по Count убывающим.
    [Fact]
    public void Different_exception_types_form_separate_groups_sorted_by_count_descending()
    {
        var events = ParseFixture("excp-mixed.ndjson");

        var result = _analyzer.Analyze(events);

        // Ожидаемые группы в excp-mixed.ndjson (без DBMSSQL — он игнорируется):
        //   MethodNotFoundException  × 2
        //   SystemException          × 2  (нормализация: два разных числа → одна группа)
        //   DataBaseException        × 2
        //   null (без Exception)     × 1
        result.TopExceptions.Should().HaveCountGreaterThanOrEqualTo(3,
            "минимум три разных типа исключений в файле");

        // Топ отсортирован по Count убывающим.
        for (var i = 1; i < result.TopExceptions.Count; i++)
        {
            result.TopExceptions[i].Count.Should()
                .BeLessOrEqualTo(result.TopExceptions[i - 1].Count,
                    "топ должен идти по убыванию Count");
        }
    }

    // (e) EXCP без полей Descr и Exception не валит анализ (толерантность к «полям-призракам»).
    [Fact]
    public void Excp_without_descr_or_exception_is_tolerated_no_throw()
    {
        var events = ParseFixture("excp-mixed.ndjson");

        // Седьмая строка excp-mixed.ndjson — EXCP без Exception и Descr.
        var act = () => _analyzer.Analyze(events);
        act.Should().NotThrow("анализатор never-throws на любом входе");

        var result = _analyzer.Analyze(events);

        // Группа с null-типом и плейсхолдером «(без описания)» должна присутствовать.
        var noDescrGroup = result.TopExceptions
            .FirstOrDefault(g => g.ExceptionType is null
                && g.NormalizedDescr == ExceptionAnalysisResult.NoDescrPlaceholder);

        noDescrGroup.Should().NotBeNull(
            "EXCP без Exception и Descr должен попасть в группу с null-типом и плейсхолдером");
        result.SkippedEvents.Should().Be(0,
            "отсутствие полей — не ошибка, never-throws, SkippedEvents = 0");
    }

    // (f) Привязка к ИБ: p:processName с GUID-суффиксом → InfobaseName без суффикса.
    [Fact]
    public void Infobase_name_normalized_from_guid_suffixed_process_name()
    {
        var events = ParseFixture("excp-mixed.ndjson");

        var result = _analyzer.Analyze(events);

        // DataBaseException в excp-mixed.ndjson имеет p:processName с GUID-суффиксом.
        var dbGroup = result.TopExceptions
            .FirstOrDefault(g => g.IsDatabaseException);

        dbGroup.Should().NotBeNull("DataBaseException должна присутствовать в топе");
        dbGroup!.InfobaseName.Should().Be("infobase01",
            "GUID-суффикс фоновой сессии должен быть отсечён TechLogProcessName.Normalize");
        dbGroup.RawProcessName.Should().Contain("296ee6d8",
            "сырое значение p:processName сохраняется без изменений");
    }

    // (g) Параметр topN уважается.
    [Fact]
    public void TopN_limits_result_count()
    {
        var events = ParseFixture("excp-mixed.ndjson");

        var result = _analyzer.Analyze(events, topN: 1);

        result.TopExceptions.Should().HaveCount(1, "topN=1 → ровно одна группа");
        // В топ-1 должна попасть группа с наибольшим Count.
        var maxCount = result.TopExceptions[0].Count;
        maxCount.Should().BeGreaterThan(0);
    }

    // (h) Не-EXCP события (DBMSSQL и др.) игнорируются → TotalExcpEvents=0 на чистом DBMSSQL.
    [Fact]
    public void Non_excp_events_are_ignored_completely()
    {
        var dbmssqlLines = ReadFixtureLines("dbmssql.ndjson");
        var tlockLines = ReadFixtureLines("tlock.ndjson");
        var allLines = dbmssqlLines.Concat(tlockLines);
        var events = _parser.ParseLines(allLines).Events;

        var result = _analyzer.Analyze(events);

        result.TotalExcpEvents.Should().Be(0, "DBMSSQL и TLOCK — не EXCP");
        result.TopExceptions.Should().BeEmpty();
        result.SkippedEvents.Should().Be(0);
    }

    // (i) Устойчивость: пустой поток не бросает и возвращает пустой результат.
    [Fact]
    public void Empty_event_stream_returns_empty_result()
    {
        var result = _analyzer.Analyze([]);

        result.TopExceptions.Should().BeEmpty();
        result.TotalExcpEvents.Should().Be(0);
        result.DatabaseExceptionEvents.Should().Be(0);
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

    // (j) NormalizeDescr: числа схлопываются к «#».
    [Theory]
    [InlineData("Lock timeout after 100 ms", "Lock timeout after 999 ms")]
    [InlineData("Object 0x1A2B not found", "Object 0xFF00 not found")]
    [InlineData("Error code 42: access denied", "Error code 7: access denied")]
    public void NormalizeDescr_different_numbers_produce_same_key(string descrA, string descrB)
    {
        var normA = ExceptionAnalyzer.NormalizeDescr(descrA);
        var normB = ExceptionAnalyzer.NormalizeDescr(descrB);

        normA.Should().Be(normB,
            "описания, отличающиеся только числовыми значениями, должны давать одинаковый ключ");
    }

    // (j) NormalizeDescr: null/пустая строка → плейсхолдер «(без описания)».
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeDescr_null_or_empty_returns_placeholder(string? descr)
    {
        var result = ExceptionAnalyzer.NormalizeDescr(descr);

        result.Should().Be(ExceptionAnalysisResult.NoDescrPlaceholder,
            "отсутствующий/пустой Descr должен возвращать плейсхолдер «(без описания)»");
    }

    // (j) NormalizeDescr: лишние пробелы схлопываются.
    [Fact]
    public void NormalizeDescr_collapses_whitespace()
    {
        var result = ExceptionAnalyzer.NormalizeDescr("Lock  request   timeout");

        result.Should().NotContain("  ", "лишние пробелы должны быть схлопнуты");
        result.Should().Be(result.Trim(), "результат без ведущих/хвостовых пробелов");
    }

    // Дополнительный тест: DatabaseExceptionEvents считается корректно.
    [Fact]
    public void Database_exception_events_counter_is_accurate()
    {
        var events = ParseFixture("excp-mixed.ndjson");

        var result = _analyzer.Analyze(events);

        // В excp-mixed.ndjson две строки с DataBaseException.
        result.DatabaseExceptionEvents.Should().Be(2,
            "две записи DataBaseException в excp-mixed.ndjson");
    }

    // Дополнительный тест: FirstTs и LastTs корректно заполняются для группы из двух EXCP.
    [Fact]
    public void Group_first_and_last_ts_are_captured()
    {
        var events = ParseFixture("excp.ndjson");

        var result = _analyzer.Analyze(events);

        result.TopExceptions.Should().HaveCount(1);
        var group = result.TopExceptions[0];
        group.FirstTs.Should().Be("2026-06-20T03:07:10.000000",
            "FirstTs — метка первого вхождения");
        group.LastTs.Should().Be("2026-06-20T03:07:10.000200",
            "LastTs — метка последнего вхождения");
    }
}
