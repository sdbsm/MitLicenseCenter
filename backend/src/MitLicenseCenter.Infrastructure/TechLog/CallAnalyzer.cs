using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Анализатор серверных вызовов 1С (MLC-249, трек «Расследование производительности»).
// Реализует ICallAnalyzer: потребляет IEnumerable<TechLogEvent>, строит CallAnalysisResult.
// internal sealed — зеркаль SlowQueryAnalyzer (тот же слой и стиль, never-throws).
//
// ГРАНИЦА (40_TECHLOG §8): ТОЛЬКО события CALL. Прочее (DBMSSQL, TLOCK, SDBL, EXCP…) игнорируется.
//
// ⚠ У CALL НЕТ p:processName (40_TECHLOG §8) → привязки к арендатору по процессу нет (НЕ нормализуем
// процесс, InfobaseName не вычисляем). Группировка — по сырому Context (стек 1С): в отличие от
// долгих запросов (SlowQueryAnalyzer группирует по нормализованной форме SQL), у CALL группа = «какая
// операция 1С» (контекст). Если Context пуст — ключ группы = имя метода; и метод пуст → вызов в агрегат
// не идёт (нечего группировать).
//
// Агрегат «много мелких» (зеркаль SlowQueryAnalyzer MLC-248): TopCalls гейтит по thresholdMicroseconds
// (единичные тяжёлые вызовы), а SimilarGroups строится по ВСЕМ CALL (НЕЗАВИСИМО от порога) — типовая
// медленность «между запросами» (тысячи быстрых вызовов одного контекста) всплывает суммарным временем,
// даже когда ни один вызов не длиннее порога. Сортировка групп по TotalDurationMicroseconds убыв., лимит topN.
// Стримовый аккумулятор (GroupAccumulator) — НЕ материализуем все события (журналы в гигабайты).
internal sealed class CallAnalyzer : ICallAnalyzer
{
    public CallAnalysisResult Analyze(
        IEnumerable<TechLogEvent> events,
        long thresholdMicroseconds = 1_000_000,
        int topN = 50)
    {
        ArgumentNullException.ThrowIfNull(events);

        var aboveThreshold = new List<CallEntry>();
        var totalCall = 0;
        var skipped = 0;

        // Аккумулятор групп — по ВСЕМ CALL (НЕ только прошедшим порог): «много мелких в сумме».
        // Лёгкое состояние на контекст (счётчик + суммы), стримово, без материализации каждого события.
        var groups = new Dictionary<string, GroupAccumulator>(StringComparer.Ordinal);

        foreach (var ev in events)
        {
            // Только CALL — всё остальное игнорируем.
            if (!string.Equals(ev.Name, "CALL", StringComparison.Ordinal))
            {
                continue;
            }

            totalCall++;

            try
            {
                // Длительность обязана быть — без неё ни в топ, ни в агрегат.
                var durationMicros = ev.DurationMicroseconds;
                if (durationMicros is null)
                {
                    skipped++;
                    continue;
                }

                var durationSeconds = ev.DurationSeconds ?? durationMicros.Value / 1_000_000d;

                var context = ev.First("Context");
                // Имя вызванного метода/интерфейса: первое непустое из Method / MName / IName.
                var method = FirstNonEmpty(ev.First("Method"), ev.First("MName"), ev.First("IName"));

                // Агрегат «по контексту» — НЕЗАВИСИМ от порога: ключ = Context (или метод, если Context пуст).
                // Нет ни Context, ни метода → группировать нечего (пропуск из агрегата, но не skipped: это
                // не ошибка — событие учтено в totalCall и в топе при достаточной длительности).
                var groupKey = !string.IsNullOrWhiteSpace(context)
                    ? context
                    : (!string.IsNullOrWhiteSpace(method) ? method : null);
                if (groupKey is not null)
                {
                    if (!groups.TryGetValue(groupKey, out var acc))
                    {
                        acc = new GroupAccumulator(groupKey);
                        groups[groupKey] = acc;
                    }

                    acc.Add(durationMicros.Value, durationSeconds);
                }

                // Порог: под-пороговые в TopCalls не идут (единичные тяжёлые), но уже учтены в агрегате выше.
                if (durationMicros.Value < thresholdMicroseconds)
                {
                    continue;
                }

                aboveThreshold.Add(new CallEntry
                {
                    Ts = ev.Ts,
                    DurationMicroseconds = durationMicros.Value,
                    DurationSeconds = durationSeconds,
                    Context = context,
                    Method = method,
                    CpuTime = ev.First("CpuTime"),
                    Memory = ev.First("Memory"),
                });
            }
            catch
            {
                // never-throws: любое непредвиденное исключение при разборе одного события
                // не ломает весь анализ — просто пропускаем (40_TECHLOG §7).
                skipped++;
            }
        }

        // Топ-N по длительности убывающим (единичные тяжёлые, гейт по порогу).
        var topCalls = aboveThreshold
            .OrderByDescending(e => e.DurationMicroseconds)
            .Take(topN)
            .ToList();

        // Группы по контексту: Count/Total/Max — по ВСЕМ CALL. Сортировка по суммарному времени убыв.; topN.
        var similarGroups = groups.Values
            .OrderByDescending(g => g.TotalDurationMicroseconds)
            .Take(topN)
            .Select(g => new CallGroup
            {
                Context = g.Context,
                Count = g.Count,
                TotalDurationMicroseconds = g.TotalDurationMicroseconds,
                MaxDurationMicroseconds = g.MaxDurationMicroseconds,
                TotalDurationSeconds = g.TotalDurationSeconds,
                MaxDurationSeconds = g.MaxDurationSeconds,
            })
            .ToList();

        return new CallAnalysisResult
        {
            TopCalls = topCalls,
            SimilarGroups = similarGroups,
            TotalCallEvents = totalCall,
            EventsAboveThreshold = aboveThreshold.Count,
            SkippedEvents = skipped,
        };
    }

    // Первое непустое значение из списка (имя метода берём из Method/MName/IName в этом порядке).
    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v;
            }
        }

        return null;
    }

    // Лёгкий аккумулятор группы вызовов по контексту: копит счётчик и суммы стримово, не держа
    // отдельные события в памяти (важно на журналах в гигабайты). Зеркаль SlowQueryAnalyzer.
    private sealed class GroupAccumulator(string context)
    {
        public string Context { get; } = context;
        public int Count { get; private set; }
        public long TotalDurationMicroseconds { get; private set; }
        public long MaxDurationMicroseconds { get; private set; }
        public double TotalDurationSeconds { get; private set; }
        public double MaxDurationSeconds { get; private set; }

        public void Add(long durationMicros, double durationSeconds)
        {
            Count++;
            TotalDurationMicroseconds += durationMicros;
            TotalDurationSeconds += durationSeconds;
            if (durationMicros > MaxDurationMicroseconds)
            {
                MaxDurationMicroseconds = durationMicros;
            }

            if (durationSeconds > MaxDurationSeconds)
            {
                MaxDurationSeconds = durationSeconds;
            }
        }
    }
}
