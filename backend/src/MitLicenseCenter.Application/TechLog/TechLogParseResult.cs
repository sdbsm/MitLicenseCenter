namespace MitLicenseCenter.Application.TechLog;

// Результат потокового разбора NDJSON-ТЖ (40_TECHLOG §4/§7). Несёт ленивую последовательность событий
// и счётчик пропущенных строк — чтобы вызывающий код видел деградацию (битые/неполные строки парсер
// не бросает, а пропускает; принцип «never throws» слоя ТЖ).
//
// ВАЖНО: Events — ЛЕНИВАЯ последовательность (память не растёт на больших журналах). SkippedLines
// и ProcessedLines заполняются ПО ХОДУ перечисления Events и принимают финальное значение ТОЛЬКО
// после того, как Events перечислена до конца. До начала перечисления счётчики = 0.
public sealed class TechLogParseResult
{
    private readonly TechLogParseCounter _counter;

    public TechLogParseResult(IEnumerable<TechLogEvent> events, TechLogParseCounter counter)
    {
        Events = events ?? throw new ArgumentNullException(nameof(events));
        _counter = counter ?? throw new ArgumentNullException(nameof(counter));
    }

    // Ленивая последовательность распарсенных событий (пустые и битые строки в неё не попадают).
    public IEnumerable<TechLogEvent> Events { get; }

    // Сколько строк пропущено как непарсимые (пустые/только пробелы/невалидный JSON/не объект).
    // Актуально после полного перечисления Events.
    public int SkippedLines => _counter.Skipped;

    // Сколько строк успешно превращено в события. Актуально после полного перечисления Events.
    public int ProcessedLines => _counter.Processed;
}

// Изменяемый счётчик прогресса разбора, общий между результатом и ленивым итератором парсера (итератор
// инкрементит его по мере выдачи событий). Публичный, т.к. реализация парсера живёт в другой сборке.
public sealed class TechLogParseCounter
{
    public int Skipped { get; set; }
    public int Processed { get; set; }
}
