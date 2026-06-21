using System.Text;
using FluentAssertions;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Infrastructure.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-249: анализатор серверных вызовов 1С из событий CALL.
// ГРАНИЦА: ТОЛЬКО CALL. Все остальные события игнорируются.
// Тесты кодируют семантику 40_TECHLOG §4/§7/§8:
//   • порог длительности — в анализаторе (для TopCalls); агрегат SimilarGroups независим от порога;
//   • группировка по сырому Context (стек 1С), а не по нормализованному SQL;
//   • у CALL НЕТ p:processName → InfobaseName не вычисляем;
//   • «поля-призраки»: Context/Method/duration могут отсутствовать (never-throws);
//   • метод — первое непустое из Method/MName/IName.
// Фикстуры: call.ndjson — два CALL (60005971 µs с методом + пустой duration);
//           call-slow.ndjson — 3 CALL одного контекста (5/7/3 c) + другой контекст (2 c) +
//           вызов без Context (метод MName, 0.5 c) + CALL без duration + CALL без Context/метода (0.1 c).
public sealed class CallAnalyzerTests
{
    private readonly TechLogParser _parser = new();
    private readonly CallAnalyzer _analyzer = new();

    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "TechLog", "Fixtures", name);

    private static string[] ReadFixtureLines(string name)
        => File.ReadAllLines(FixturePath(name), Encoding.UTF8);

    private IEnumerable<TechLogEvent> ParseFixture(string name)
        => _parser.ParseLines(ReadFixtureLines(name)).Events;

    // (a) Долгие вызовы (≥ порога) попадают в TopCalls; короткие/без длительности — нет.
    //     call-slow.ndjson при пороге 1 000 000 µs: проходят 5/7/3/2 c (4 шт); 0.5 c и 0.1 c — ниже;
    //     CALL без duration — пропущен (skipped).
    [Fact]
    public void Long_calls_above_threshold_appear_in_top_calls()
    {
        var events = ParseFixture("call-slow.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000);

        result.TotalCallEvents.Should().Be(7, "семь CALL-событий в файле");
        result.EventsAboveThreshold.Should().Be(4, "5/7/3/2 c ≥ порога 1 c");
        result.TopCalls.Should().HaveCount(4);
        result.SkippedEvents.Should().Be(1, "CALL без duration пропущен (never-throws)");
    }

    // (b) TopCalls отсортированы по длительности убывающим.
    [Fact]
    public void Top_calls_are_sorted_by_duration_descending()
    {
        var events = ParseFixture("call-slow.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000);

        result.TopCalls[0].DurationMicroseconds.Should().Be(7_000_000, "самый долгий — 7 c");
        for (var i = 1; i < result.TopCalls.Count; i++)
        {
            result.TopCalls[i].DurationMicroseconds.Should()
                .BeLessOrEqualTo(result.TopCalls[i - 1].DurationMicroseconds);
        }
    }

    // (c) Поля записи (Context, Method, CpuTime, Memory) читаются из полной записи.
    //     Метод — первое непустое из Method/MName/IName (у первой записи call-slow задан Method).
    [Fact]
    public void Full_record_fields_context_method_cpu_memory_are_captured()
    {
        var events = ParseFixture("call-slow.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000);

        var entry = result.TopCalls.First(e => e.DurationMicroseconds == 5_000_000);
        entry.Context.Should().Be("Документ.ЗакрытиеМесяца.МодульМенеджера:120");
        entry.Method.Should().Be("Posting", "первое непустое из Method/MName/IName");
        entry.CpuTime.Should().Be("3000000");
        entry.Memory.Should().Be("4194304");
    }

    // (c2) Метод берётся из MName, когда Method отсутствует (вызов без Context, но с MName).
    [Fact]
    public void Method_falls_back_to_mname_when_method_absent()
    {
        var events = ParseFixture("call-slow.ndjson");

        // Порог 0 — пройдут все с длительностью, включая 0.5 c вызов с MName.
        var result = _analyzer.Analyze(events, thresholdMicroseconds: 0);

        var entry = result.TopCalls.First(e => e.DurationMicroseconds == 500_000);
        entry.Context.Should().BeNull("у этого вызова Context отсутствует");
        entry.Method.Should().Be("ИдлеХендлер", "метод взят из MName (Method/IName заданы или пусты)");
    }

    // (d) АГРЕГАТ по контексту НЕЗАВИСИМ от порога: три CALL одного контекста (5/7/3 c) → одна группа
    //     Count=3, Total=15 c, Max=7 c. CALL без duration в группу не идёт (пропущен).
    [Fact]
    public void Similar_calls_with_same_context_form_single_group_independent_of_threshold()
    {
        var events = ParseFixture("call-slow.ndjson");

        // Высокий порог 100 c: TopCalls пуст, но агрегат — нет (зеркаль MLC-248 для SlowQuery).
        var result = _analyzer.Analyze(events, thresholdMicroseconds: 100_000_000);

        result.TopCalls.Should().BeEmpty("ни один вызов не длиннее 100 c");
        var group = result.SimilarGroups
            .FirstOrDefault(g => g.Context.Contains("МодульМенеджера", StringComparison.Ordinal));
        group.Should().NotBeNull("три вызова одного контекста должны схлопнуться в группу");
        group!.Count.Should().Be(3, "три вызова с duration; четвёртый (без duration) не учтён");
        group.TotalDurationMicroseconds.Should().Be(5_000_000 + 7_000_000 + 3_000_000);
        group.MaxDurationMicroseconds.Should().Be(7_000_000);
    }

    // (e) ЯДРО (зеркаль SlowQuery MLC-248): «много быстрых вызовов одного контекста» → группа есть,
    //     TopCalls пуст при высоком пороге. Кейс «закрытия месяца»: вычисления между запросами.
    [Fact]
    public void Many_fast_calls_form_group_even_when_top_is_empty_at_high_threshold()
    {
        // 500 быстрых вызовов одного контекста по 40 000 µs (0.04 c) — ни один не дотягивает до 1 c.
        var lines = Enumerable.Range(0, 500).Select(i =>
            "{\"ts\":\"2026-06-20T05:00:00.000000\",\"duration\":\"40000\",\"name\":\"CALL\"," +
            "\"process\":\"rphost\"," +
            "\"Context\":\"Документ.ЗакрытиеМесяца.ОбработкаПроведения:88\"," +
            $"\"Method\":\"Posting\",\"CallID\":\"{i}\"}}");
        var events = _parser.ParseLines(lines).Events;

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000);

        result.TopCalls.Should().BeEmpty("ни один вызов не длиннее порога 1 c");
        result.EventsAboveThreshold.Should().Be(0);
        result.TotalCallEvents.Should().Be(500);

        result.SimilarGroups.Should().ContainSingle("все 500 — один контекст");
        result.SimilarGroups[0].Count.Should().Be(500);
        result.SimilarGroups[0].TotalDurationMicroseconds.Should().Be(500L * 40_000, "500 × 40 000 µs = 20 c");
        result.SimilarGroups[0].MaxDurationMicroseconds.Should().Be(40_000);
    }

    // (f) Группы отсортированы по суммарной длительности убыв.
    [Fact]
    public void Groups_are_sorted_by_total_duration_descending()
    {
        var events = ParseFixture("call-slow.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 0);

        result.SimilarGroups.Should().HaveCountGreaterThan(1);
        result.SimilarGroups[0].Context.Should().Contain("МодульМенеджера",
            "группа с суммарными 15 c — первая");
        for (var i = 1; i < result.SimilarGroups.Count; i++)
        {
            result.SimilarGroups[i].TotalDurationMicroseconds.Should()
                .BeLessOrEqualTo(result.SimilarGroups[i - 1].TotalDurationMicroseconds);
        }
    }

    // (g) CALL без Context: ключ группы = метод. CALL без Context и без метода → в агрегат не идёт,
    //     но это НЕ ошибка (не skipped) — событие учтено в TotalCallEvents.
    [Fact]
    public void Call_without_context_groups_by_method_or_excluded_when_no_method()
    {
        var events = ParseFixture("call-slow.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 0);

        // Вызов без Context, но с MName="ИдлеХендлер" → группа по методу.
        result.SimilarGroups.Should().Contain(g => g.Context == "ИдлеХендлер",
            "вызов без Context группируется по имени метода");

        // Вызов без Context и без метода (0.1 c, CallID=506) → НЕ в группах.
        result.SimilarGroups.Should().NotContain(g => g.Count > 0 && g.Context == string.Empty);
        result.SkippedEvents.Should().Be(1, "пропущен только CALL без duration; без Context/метода — не ошибка");
    }

    // (h) Не-CALL события игнорируются полностью.
    [Fact]
    public void Non_call_events_are_ignored_completely()
    {
        var dbmssqlLines = ReadFixtureLines("dbmssql.ndjson");
        var tlockLines = ReadFixtureLines("tlock.ndjson");
        var events = _parser.ParseLines(dbmssqlLines.Concat(tlockLines)).Events;

        var result = _analyzer.Analyze(events);

        result.TotalCallEvents.Should().Be(0, "DBMSSQL и TLOCK — не CALL");
        result.TopCalls.Should().BeEmpty();
        result.SimilarGroups.Should().BeEmpty();
        result.SkippedEvents.Should().Be(0);
    }

    // (i) Устойчивость: пустой поток.
    [Fact]
    public void Empty_event_stream_returns_empty_result()
    {
        var result = _analyzer.Analyze([]);

        result.TopCalls.Should().BeEmpty();
        result.SimilarGroups.Should().BeEmpty();
        result.TotalCallEvents.Should().Be(0);
        result.EventsAboveThreshold.Should().Be(0);
        result.SkippedEvents.Should().Be(0);
    }

    // (i2) Устойчивость: смешанный поток с BOM не бросает (never-throws).
    [Fact]
    public void Mixed_stream_with_bom_does_not_throw()
    {
        var events = ParseFixture("mixed-with-bom.ndjson");

        var act = () => _analyzer.Analyze(events);
        act.Should().NotThrow("анализатор never-throws на любом входе");
    }

    // (j) topN ограничивает и TopCalls, и SimilarGroups.
    [Fact]
    public void TopN_limits_top_calls_and_groups()
    {
        var events = ParseFixture("call-slow.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 0, topN: 1);

        result.TopCalls.Should().HaveCount(1, "topN=1 → одна запись в топе");
        result.TopCalls[0].DurationMicroseconds.Should().Be(7_000_000, "самый долгий");
        result.SimilarGroups.Should().HaveCount(1, "topN=1 → одна группа");
    }

    // (k) Базовый call.ndjson: первый CALL (60 c) проходит порог; второй (без duration) пропущен.
    [Fact]
    public void Base_call_fixture_long_call_in_top_empty_duration_skipped()
    {
        var events = ParseFixture("call.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 1_000_000);

        result.TotalCallEvents.Should().Be(2);
        result.TopCalls.Should().HaveCount(1, "первый CALL 60005971 µs ≥ порога");
        result.TopCalls[0].DurationMicroseconds.Should().Be(60_005_971);
        result.TopCalls[0].Method.Should().Be("Execute", "Method задан");
        result.SkippedEvents.Should().Be(1, "второй CALL без duration пропущен");
    }

    // ─── MLC-252 A: чистка CALL-группировки ──────────────────────────────────────────────────────
    // Стенд-приёмка 1.2 (параллельные нагрузки) показала мусорные группы `0`/`5`/`7`/`83`/`methodsCount`/
    // `Release` (числовые коды/служебные токены) и некорректный итог суммой вложенного времени.
    // Фикстура call-noisy.ndjson: 5 шумных ключей (0/5/83/methodsCount/Release) + вызов без Context/метода
    // + 1 осмысленный Context (3 c, "Документ.ЗакрытиеМесяца…") + 1 шумной "7" длительностью 45 c (обёртка).

    // (252-a) Числовые коды и служебные токены НЕ создают отдельных групп — все сводятся в ОДНУ группу
    //         «контекст не указан» (IsUnspecified). Осмысленный Context — отдельной группой.
    [Fact]
    public void Numeric_and_denylist_keys_collapse_into_single_unspecified_group()
    {
        var events = ParseFixture("call-noisy.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 0);

        // Ровно две группы: осмысленный контекст + единая «контекст не указан».
        result.SimilarGroups.Should().HaveCount(2,
            "числовые/служебные ключи не плодят группы — всё в одну «контекст не указан»");

        // Нет групп с ключами-мусором 0/5/7/83/methodsCount/Release.
        var noisyKeys = new[] { "0", "5", "7", "83", "methodsCount", "Release" };
        result.SimilarGroups.Should().NotContain(
            g => noisyKeys.Contains(g.Context),
            "мусорные числовые/служебные ключи не должны быть ключами групп");

        // Единая «контекст не указан» группа существует и помечена IsUnspecified.
        var unspecified = result.SimilarGroups.FirstOrDefault(g => g.IsUnspecified);
        unspecified.Should().NotBeNull("числовые/служебные/пустые сведены в одну группу «контекст не указан»");
        // В неё вошли: 0/5/83/methodsCount/Release/605(без метода)/7 = 7 вызовов.
        unspecified!.Count.Should().Be(7, "все шумные + безымянный вызов — в одной группе");

        // Осмысленный контекст — отдельная не-unspecified группа.
        var real = result.SimilarGroups.FirstOrDefault(
            g => g.Context.Contains("МодульМенеджера", StringComparison.Ordinal));
        real.Should().NotBeNull("осмысленный Context — отдельной группой");
        real!.IsUnspecified.Should().BeFalse();
        real.Count.Should().Be(1);
    }

    // (252-b) Обёртка: вызов без осмысленного контекста с очень большой длительностью (45 c ≥ порога
    //         обёртки 30 c) помечается IsWrapper и уходит В КОНЕЦ сортировки (не доминирует в топе).
    [Fact]
    public void Long_unspecified_call_is_flagged_as_wrapper_and_sorted_last()
    {
        var events = ParseFixture("call-noisy.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 0);

        var wrapper = result.SimilarGroups.FirstOrDefault(g => g.IsWrapper);
        wrapper.Should().NotBeNull("вызов 45 c без осмысленного контекста — обёртка");
        wrapper!.IsUnspecified.Should().BeTrue("обёртка всегда без осмысленного контекста");

        // Несмотря на самое большое суммарное время (45 c доминирует), обёртка — ПОСЛЕДНЯЯ.
        result.SimilarGroups[^1].IsWrapper.Should().BeTrue(
            "обёртка вынесена в конец, чтобы не доминировать в топе");
        result.SimilarGroups[0].IsWrapper.Should().BeFalse(
            "первой идёт осмысленная группа, а не обёртка");
    }

    // (252-c) Осмысленный метод (не числовой, не денлист) остаётся отдельной группой (не «контекст не
    //         указан»). call-slow.ndjson: вызов с MName="ИдлеХендлер" — осмысленный.
    [Fact]
    public void Meaningful_named_method_stays_its_own_group()
    {
        var events = ParseFixture("call-slow.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 0);

        var byMethod = result.SimilarGroups.FirstOrDefault(g => g.Context == "ИдлеХендлер");
        byMethod.Should().NotBeNull("осмысленное имя метода — отдельная группа");
        byMethod!.IsUnspecified.Should().BeFalse("имя метода — осмысленный ключ, не «контекст не указан»");
    }

    // (252-d) Группы без обёртки сортируются по собственному (gross) времени убыв.; общая сумма НЕ считается
    //         (нельзя складывать вложенное). Проверяем только корректную сортировку не-обёрток.
    [Fact]
    public void Non_wrapper_groups_sorted_by_own_gross_duration_descending()
    {
        var events = ParseFixture("call-slow.ndjson");

        var result = _analyzer.Analyze(events, thresholdMicroseconds: 0);

        var nonWrappers = result.SimilarGroups.Where(g => !g.IsWrapper).ToList();
        for (var i = 1; i < nonWrappers.Count; i++)
        {
            nonWrappers[i].TotalDurationMicroseconds.Should()
                .BeLessOrEqualTo(nonWrappers[i - 1].TotalDurationMicroseconds);
        }
    }
}
