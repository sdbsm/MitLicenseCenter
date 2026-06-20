using System.Text;
using FluentAssertions;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Infrastructure.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-233 (этап B): анализатор управляемых блокировок 1С (TLOCK/TTIMEOUT/TDEADLOCK).
// ГРАНИЦА: ТОЛЬКО 1С-уровень (менеджер блокировок платформы); СУБД-уровень (<dbmslocks/>/lkX) —
// MLC-236. Тесты кодируют семантику 40_TECHLOG §5/§7/§8 и устойчивость («поля-призраки»).
//
// Фикстуры TTIMEOUT/TDEADLOCK: точная структура полей подлежит подтверждению на стенде 8.5
// (приёмка владельца) — собрано по семантике 40_TECHLOG §5. Фикстура tlock.ndjson — реальные
// структуры полей со стенда 8.5 (MLC-229, 40_TECHLOG §8), значения обезличены.
public sealed class LockTreeAnalyzerTests
{
    private readonly TechLogParser _parser = new();
    private readonly LockTreeAnalyzer _analyzer = new();

    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "TechLog", "Fixtures", name);

    private static string[] ReadFixtureLines(string name)
        => File.ReadAllLines(FixturePath(name), Encoding.UTF8);

    private IEnumerable<TechLogEvent> ParseFixture(string name)
        => _parser.ParseLines(ReadFixtureLines(name)).Events;

    // (a) Из tlock.ndjson строится ребро ожидания: вторая запись содержит WaitConnections="3",
    //     первая — WaitConnections="" (взята без ожидания, ребро не строится).
    [Fact]
    public void Builds_wait_edge_from_tlock_with_wait_connections()
    {
        var events = ParseFixture("tlock.ndjson");

        var result = _analyzer.Analyze(events);

        result.WaitEdges.Should().HaveCount(1, "только вторая запись имеет непустой WaitConnections");
        var edge = result.WaitEdges[0];
        edge.BlockingConnections.Should().Be("3");
        edge.WaitingSessionId.Should().Be("2");
        result.TlockEventsProcessed.Should().Be(2, "обе строки — TLOCK, обе считаются");
    }

    // (b) Режим блокировки (Locks) и ресурс (Regions) из tlock.ndjson читаются корректно.
    [Fact]
    public void Reads_lock_mode_and_regions_from_tlock()
    {
        var events = ParseFixture("tlock.ndjson");

        var result = _analyzer.Analyze(events);

        var edge = result.WaitEdges.Should().ContainSingle().Subject;
        edge.LockMode.Should().Be("Shared");
        edge.Regions.Should().Be("AccumulationRegister.Stock");
    }

    // (c) Длительность ожидания нормализована µs→сек (tlock.ndjson, вторая запись: 30000000 µs = 30 с).
    [Fact]
    public void Normalizes_wait_duration_microseconds_to_seconds()
    {
        var events = ParseFixture("tlock.ndjson");

        var result = _analyzer.Analyze(events);

        var edge = result.WaitEdges.Should().ContainSingle().Subject;
        edge.WaitDurationSeconds.Should().BeApproximately(30.0, 1e-6);
    }

    // (d) Привязка к ИБ через p:processName, включая нормализацию суффикса-GUID.
    // tlock.ndjson: p:processName="infobase01" (голое имя) → InfobaseName="infobase01".
    [Fact]
    public void Infobase_name_from_process_name_plain()
    {
        var events = ParseFixture("tlock.ndjson");

        var result = _analyzer.Analyze(events);

        var edge = result.WaitEdges.Should().ContainSingle().Subject;
        edge.InfobaseName.Should().Be("infobase01");
        edge.RawProcessName.Should().Be("infobase01");
    }

    // (d) Нормализация суффикса-GUID к базовому имени ИБ.
    // ttimeout.ndjson вторая запись: p:processName="infobase01_296ee6d8-1234-5678-abcd-ef0123456789"
    [Fact]
    public void Normalizes_guid_suffix_in_process_name_for_background_session()
    {
        var events = ParseFixture("ttimeout.ndjson");

        var result = _analyzer.Analyze(events);

        // Вторая запись с GUID-суффиксом
        var bgTimeout = result.Timeouts.Should().HaveCount(2).And.Subject
            .FirstOrDefault(t => t.RawProcessName?.Contains("296ee6d8") == true);
        bgTimeout.Should().NotBeNull();
        bgTimeout!.InfobaseName.Should().Be("infobase01",
            "GUID-суффикс фоновой сессии должен быть отсечён");
        bgTimeout.RawProcessName.Should().Contain("296ee6d8",
            "сырое значение p:processName сохраняется");
    }

    // (e) TTIMEOUT попадает в список таймаутов (из ttimeout.ndjson — 2 записи).
    [Fact]
    public void Ttimeout_events_populate_timeouts_list()
    {
        var events = ParseFixture("ttimeout.ndjson");

        var result = _analyzer.Analyze(events);

        result.Timeouts.Should().HaveCount(2);
        result.WaitEdges.Should().BeEmpty("TTIMEOUT — не TLOCK, в рёбрах не появляется");
        result.Deadlocks.Should().BeEmpty();

        var first = result.Timeouts[0];
        first.SessionId.Should().Be("3");
        first.Regions.Should().Be("AccumulationRegister.Stock");
        first.LockMode.Should().Be("Exclusive");
        first.WaitDurationSeconds.Should().BeApproximately(30.0, 1e-6);
    }

    // (f) TDEADLOCK попадает в список дедлоков (из tdeadlock.ndjson — 2 записи-участника).
    [Fact]
    public void Tdeadlock_events_populate_deadlocks_list()
    {
        var events = ParseFixture("tdeadlock.ndjson");

        var result = _analyzer.Analyze(events);

        result.Deadlocks.Should().HaveCount(2);
        result.WaitEdges.Should().BeEmpty("TDEADLOCK — не TLOCK, в рёбрах не появляется");
        result.Timeouts.Should().BeEmpty();

        result.Deadlocks[0].SessionId.Should().Be("5");
        result.Deadlocks[1].SessionId.Should().Be("6");
    }

    // (g) Устойчивость: TLOCK без WaitConnections (первая запись tlock.ndjson) не валит анализ,
    //     просто не строит ребро; TLOCK с полностью отсутствующими полями тоже не бросает.
    [Fact]
    public void Tlock_without_wait_connections_does_not_throw_and_creates_no_edge()
    {
        // Первая строка tlock.ndjson: WaitConnections="" (пусто — взята без ожидания)
        var line = ReadFixtureLines("tlock.ndjson")[0];
        var ev = _parser.ParseLine(line);
        ev.Should().NotBeNull();

        var act = () => _analyzer.Analyze([ev!]);
        act.Should().NotThrow();

        var result = _analyzer.Analyze([ev!]);
        result.WaitEdges.Should().BeEmpty("WaitConnections пусто → нет ожидания → нет ребра");
        result.TlockEventsProcessed.Should().Be(1);
    }

    // (g) Устойчивость: событие совсем без полей (пустой объект) не бросает.
    [Fact]
    public void Tlock_with_no_fields_does_not_throw()
    {
        var ev = _parser.ParseLine("{\"name\":\"TLOCK\"}");
        ev.Should().NotBeNull();

        var act = () => _analyzer.Analyze([ev!]);
        act.Should().NotThrow();

        var result = _analyzer.Analyze([ev!]);
        result.WaitEdges.Should().BeEmpty();
        result.TlockEventsProcessed.Should().Be(1);
    }

    // (h) Вариант имени пользователя: UserName вместо Usr распознан (40_TECHLOG §7).
    [Fact]
    public void Recognizes_username_field_as_alternative_to_usr()
    {
        // ttimeout.ndjson вторая запись: UserName="BgUser" (нет поля Usr)
        var events = ParseFixture("ttimeout.ndjson");

        var result = _analyzer.Analyze(events);

        var bgTimeout = result.Timeouts.FirstOrDefault(t => t.RawProcessName?.Contains("296ee6d8") == true);
        bgTimeout.Should().NotBeNull();
        bgTimeout!.User.Should().Be("BgUser", "UserName читается как fallback к Usr");
    }

    // (i) События НЕ-блокировочного типа (DBMSSQL из dbmssql.ndjson) игнорируются анализатором.
    [Fact]
    public void Non_lock_events_are_ignored_and_produce_no_output()
    {
        var events = ParseFixture("dbmssql.ndjson");

        var result = _analyzer.Analyze(events);

        result.WaitEdges.Should().BeEmpty();
        result.Timeouts.Should().BeEmpty();
        result.Deadlocks.Should().BeEmpty();
        result.TlockEventsProcessed.Should().Be(0);
    }

    // Смешанный поток: TLOCK + TTIMEOUT + TDEADLOCK + DBMSSQL → правильная сортировка по спискам.
    [Fact]
    public void Mixed_event_stream_routes_events_to_correct_lists()
    {
        var tlockLines = ReadFixtureLines("tlock.ndjson");      // 2 TLOCK (1 ждёт)
        var ttimeoutLines = ReadFixtureLines("ttimeout.ndjson"); // 2 TTIMEOUT
        var tdeadlockLines = ReadFixtureLines("tdeadlock.ndjson"); // 2 TDEADLOCK
        var dbmssqlLines = ReadFixtureLines("dbmssql.ndjson");  // 2 DBMSSQL (игнор)

        var allLines = tlockLines
            .Concat(ttimeoutLines)
            .Concat(tdeadlockLines)
            .Concat(dbmssqlLines);

        var events = _parser.ParseLines(allLines).Events;
        var result = _analyzer.Analyze(events);

        result.WaitEdges.Should().HaveCount(1, "один TLOCK с WaitConnections");
        result.Timeouts.Should().HaveCount(2, "два TTIMEOUT");
        result.Deadlocks.Should().HaveCount(2, "два TDEADLOCK");
        result.TlockEventsProcessed.Should().Be(2, "два TLOCK всего");
    }

    // Пустой поток — не бросает, возвращает пустой результат.
    [Fact]
    public void Empty_event_stream_returns_empty_result()
    {
        var result = _analyzer.Analyze([]);

        result.WaitEdges.Should().BeEmpty();
        result.Timeouts.Should().BeEmpty();
        result.Deadlocks.Should().BeEmpty();
        result.TlockEventsProcessed.Should().Be(0);
        result.SkippedEvents.Should().Be(0);
    }

    // NormalizeProcessName: голое имя проходит без изменений.
    [Theory]
    [InlineData("infobase01", "infobase01")]
    [InlineData("mitpro", "mitpro")]
    [InlineData("ut11_saratov", "ut11_saratov")]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void NormalizeProcessName_plain_name_unchanged(string? input, string? expected)
    {
        LockTreeAnalyzer.NormalizeProcessName(input).Should().Be(expected);
    }

    // NormalizeProcessName: суффикс-GUID отсекается.
    [Theory]
    [InlineData("infobase01_296ee6d8-1234-5678-abcd-ef0123456789", "infobase01")]
    [InlineData("ut11_saratov_aaaabbbb-cccc-dddd-eeee-ffffffffffff", "ut11_saratov")]
    [InlineData("base_00000000-0000-0000-0000-000000000000", "base")]
    public void NormalizeProcessName_strips_guid_suffix(string input, string expected)
    {
        LockTreeAnalyzer.NormalizeProcessName(input).Should().Be(expected);
    }

    // TLOCK: поле Context читается и попадает в ребро.
    [Fact]
    public void Tlock_context_field_is_captured_in_edge()
    {
        var events = ParseFixture("tlock.ndjson");
        var result = _analyzer.Analyze(events);

        var edge = result.WaitEdges.Should().ContainSingle().Subject;
        edge.Context.Should().Be("Document.Posting");
    }

    // TLOCK: поле DataBase читается и попадает в ребро.
    [Fact]
    public void Tlock_database_field_is_captured_in_edge()
    {
        var events = ParseFixture("tlock.ndjson");
        var result = _analyzer.Analyze(events);

        var edge = result.WaitEdges.Should().ContainSingle().Subject;
        edge.Database.Should().Be(@"localhost\infobase01");
    }

    // Устойчивость: поток с null-именем события не бросает (поле name отсутствует).
    [Fact]
    public void Event_with_null_name_is_skipped_without_throwing()
    {
        var ev = _parser.ParseLine("{\"duration\":\"100\",\"depth\":\"1\"}");
        ev.Should().NotBeNull();
        ev!.Name.Should().BeNull();

        var act = () => _analyzer.Analyze([ev]);
        act.Should().NotThrow();

        var result = _analyzer.Analyze([ev]);
        result.SkippedEvents.Should().Be(1);
    }
}
