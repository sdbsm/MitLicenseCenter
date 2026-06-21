namespace MitLicenseCenter.Application.TechLog;

// Результат анализа серверных вызовов 1С (MLC-249, трек «Расследование производительности»).
// Строится ICallAnalyzer из событий CALL. Зеркаль SlowQueryAnalysisResult по стилю/форме.
//
// Структура события CALL (40_TECHLOG §8, стенд 8.5): ts, duration (µs), Context (стек 1С — главное
// для группировки «какая операция»), Method/MName/IName/Interface (имя вызванного метода/интерфейса),
// Memory/MemoryPeak, CpuTime, callWait, InBytes/OutBytes, CallID.
// ⚠ У CALL НЕТ p:processName (40_TECHLOG §8) → привязки к арендатору по процессу нет (InfobaseName
// не вычисляем). CALL даёт картину «почему медленно НА СТОРОНЕ 1С» (вычисления между запросами),
// в отличие от DBMSSQL (время в СУБД).
//
// Все строки — как пришли из ТЖ (значения-строки, §4); нормализованные числа и null при отсутствии
// поля. Неизменяемые типы (records). Комментарии по-русски.

/// <summary>
/// Запись об одном серверном вызове 1С (из события CALL, 40_TECHLOG §8).
/// Каждая запись — отдельный вызов; агрегаты «похожих по контексту» — в CallGroup.
/// </summary>
public sealed record CallEntry
{
    // Метка времени события CALL (строка из поля ts, как пришла из ТЖ).
    public string? Ts { get; init; }

    // Длительность вызова в микросекундах (единица ТЖ 8.5, 40_TECHLOG §4: µs).
    // Ненулевое — только вызовы, прошедшие порог thresholdMicroseconds.
    public long DurationMicroseconds { get; init; }

    // Длительность в секундах = DurationMicroseconds / 1e6.
    public double DurationSeconds { get; init; }

    // Контекст стека 1С (поле Context, 40_TECHLOG §8). Главное для понимания «какая операция».
    // null — часто отсутствует.
    public string? Context { get; init; }

    // Имя вызванного метода/интерфейса: первое непустое из Method / MName / IName (40_TECHLOG §8).
    // null — если ни одно поле не задано («поля-призраки» §7).
    public string? Method { get; init; }

    // Процессорное время вызова в микросекундах (поле CpuTime, как пришло строкой). null — поля нет.
    // Храним как сырую строку: набор/единица CpuTime в JSON-ТЖ 8.5 точно не зафиксированы (стенд),
    // не навязываем числовую нормализацию (never-throws, толерантность §7).
    public string? CpuTime { get; init; }

    // Память вызова (поле Memory, 40_TECHLOG §8, как пришла строкой). null — поля нет.
    public string? Memory { get; init; }
}

/// <summary>
/// Группа серверных вызовов 1С по контексту (агрегат, MLC-249). Ключ группировки — сырой Context
/// (если Context пуст — имя метода; если и метод пуст — вызов в агрегат не входит).
/// Агрегат НЕЗАВИСИМ от порога (как SlowQueryGroup, MLC-248): учитывает ВСЕ CALL — типовая медленность
/// «между запросами» (тысячи быстрых вызовов одного контекста) всплывает суммарным временем, даже когда
/// ни один вызов не длиннее порога. Группы отсортированы по суммарной длительности убыв., ограничены topN.
/// </summary>
public sealed record CallGroup
{
    // Ключ группировки: сырой Context вызова (или имя метода, если Context пуст).
    public string Context { get; init; } = string.Empty;

    // Число вхождений контекста по ВСЕМ CALL (включая под-пороговые).
    public int Count { get; init; }

    // Суммарная длительность всех вхождений в микросекундах.
    public long TotalDurationMicroseconds { get; init; }

    // Максимальная длительность среди вхождений в микросекундах.
    public long MaxDurationMicroseconds { get; init; }

    // Суммарная длительность в секундах.
    public double TotalDurationSeconds { get; init; }

    // Максимальная длительность в секундах.
    public double MaxDurationSeconds { get; init; }
}

/// <summary>
/// Итоговый результат анализа серверных вызовов 1С (CALL).
/// Строится ICallAnalyzer из произвольного потока TechLogEvent.
/// </summary>
public sealed class CallAnalysisResult
{
    // Топ долгих вызовов, отсортированный по длительности убывающим (топ-N).
    // Только CALL с длительностью ≥ порога (thresholdMicroseconds).
    public IReadOnlyList<CallEntry> TopCalls { get; init; } = [];

    // Группы вызовов по контексту (по ВСЕМ CALL, НЕЗАВИСИМО от порога TopCalls — «много мелких в сумме»).
    // Отсортированы по суммарной длительности убывающим, ограничены topN. Вызовы без Context и без метода
    // в группировку не входят (нечего группировать).
    public IReadOnlyList<CallGroup> SimilarGroups { get; init; } = [];

    // Сколько CALL-событий обнаружено в потоке (до фильтрации по порогу).
    public int TotalCallEvents { get; init; }

    // Сколько CALL-событий прошло порог длительности.
    public int EventsAboveThreshold { get; init; }

    // Сколько событий пропущено (нет длительности / непредвиденная ошибка разбора, устойчивость §7).
    public int SkippedEvents { get; init; }
}
