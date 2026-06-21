using System.Text.RegularExpressions;
using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Анализатор серверных вызовов 1С (MLC-249, чистка MLC-252; трек «Расследование производительности»).
// Реализует ICallAnalyzer: потребляет IEnumerable<TechLogEvent>, строит CallAnalysisResult.
// internal sealed — зеркаль SlowQueryAnalyzer (тот же слой и стиль, never-throws).
//
// ГРАНИЦА (40_TECHLOG §8): ТОЛЬКО события CALL. Прочее (DBMSSQL, TLOCK, SDBL, EXCP…) игнорируется.
//
// ⚠ У CALL НЕТ p:processName (40_TECHLOG §8) → привязки к арендатору по процессу нет (НЕ нормализуем
// процесс, InfobaseName не вычисляем). Группировка — по осмысленному Context (стек 1С): в отличие от
// долгих запросов (SlowQueryAnalyzer группирует по нормализованной форме SQL), у CALL группа = «какая
// операция 1С» (контекст).
//
// ── MLC-252 A-1: ОСМЫСЛЕННЫЙ ключ группы (стенд-приёмка 1.2). ──────────────────────────────────────
// На живом параллельном прогоне группировка падала на сырой Method/MName/IName и плодила мусорные группы
// с ключами `0`,`5`,`7`,`83`,`methodsCount`,`Release` — это числовые коды/служебные токены, НЕ контекст.
// Правило ключа группы теперь:
//   1) предпочесть Context (стек 1С — «Форма.Вызов : …»/«ОбщийМодуль.Вызов : …»/«…РегламентноеЗадание»);
//   2) иначе именованный метод (Method/MName/IName), НО только осмысленный: не чисто числовой (^\d+$)
//      и не в денлисте служебных токенов (methodsCount/Release/methods/…);
//   3) иначе — единая группа «контекст не указан» (одна на всё, IsUnspecified=true), а НЕ группа на каждый
//      числовой код. Числовые/денлист-значения тоже сводятся сюда.
//
// ── MLC-252 A-2: время CALL ВЛОЖЕННОЕ (gross), НЕ суммируется. ─────────────────────────────────────
// Родительский серверный вызов содержит вложенные SQL/под-вызовы — его длительность их включает. Поэтому
// «общий итог по группам» (бывшая FE-строка «Вызовы всего») некорректен и убран. Группы сортируются по
// собственному gross-времени. Верхний фоновый вызов-«обёртка» (большая длительность + неосмысленный
// контекст) помечается IsWrapper=true и выносится в конец сортировки, чтобы не доминировал в топе.
//
// Стримовый аккумулятор (GroupAccumulator) — НЕ материализуем все события (журналы в гигабайты).
internal sealed partial class CallAnalyzer : ICallAnalyzer
{
    // Чисто числовой токен (напр. «0», «5», «83») — это код/идентификатор, а не имя метода. Не ключ группы.
    [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericTokenRegex();

    // Денлист служебных токенов, которые ТЖ кладёт в Method/MName/IName, но смыслом операции они не являются
    // (наблюдались на стенде: methodsCount/Release; methods — родственный). Сравнение Ordinal-IgnoreCase
    // (регистр на стенде варьируется). Список узкий и прокомментирован — расширять по находкам, не вслепую.
    private static readonly HashSet<string> ServiceTokenDenylist = new(StringComparer.OrdinalIgnoreCase)
    {
        "methodsCount",
        "methods",
        "Release",
    };

    // MLC-252 A-2: порог «обёртки». Вызов без осмысленного контекста (числовой/служебный/пустой ключ),
    // чья длительность очень велика (≥ этого), скорее всего фоновый long-poll / точка входа, чьё окно
    // содержит почти всё расследование. Помечаем такую группу IsWrapper, чтобы она не доминировала в топе.
    // 30 c = заведомо «обёрточная» длительность для серверного вызова (фоновые long-poll на стенде — 60 c).
    private const long WrapperDurationThresholdMicros = 30_000_000;

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

                // MLC-252 A-1: осмысленный ключ группы. Числовые/служебные/пустые → «контекст не указан»
                // (одна группа на все, не плодить по числам). isUnspecified помечает эту группу.
                var (groupKey, isUnspecified) = ResolveGroupKey(context, method);

                if (!groups.TryGetValue(groupKey, out var acc))
                {
                    acc = new GroupAccumulator(groupKey, isUnspecified);
                    groups[groupKey] = acc;
                }

                acc.Add(durationMicros.Value, durationSeconds);

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

        // Группы по контексту: Count/Total/Max — по ВСЕМ CALL. Сортировка по собственному (gross) времени
        // убыв.; обёртки (большая длительность + неосмысленный контекст) — В КОНЕЦ, чтобы не доминировали.
        var similarGroups = groups.Values
            .Select(g => new CallGroup
            {
                Context = g.Context,
                Count = g.Count,
                TotalDurationMicroseconds = g.TotalDurationMicroseconds,
                MaxDurationMicroseconds = g.MaxDurationMicroseconds,
                TotalDurationSeconds = g.TotalDurationSeconds,
                MaxDurationSeconds = g.MaxDurationSeconds,
                IsUnspecified = g.IsUnspecified,
                // Обёртка: неосмысленный контекст + хотя бы один вызов «обёрточной» длительности.
                IsWrapper = g.IsUnspecified && g.MaxDurationMicroseconds >= WrapperDurationThresholdMicros,
            })
            .OrderBy(g => g.IsWrapper) // false (0) раньше true (1) — обёртки в конец.
            .ThenByDescending(g => g.TotalDurationMicroseconds)
            .Take(topN)
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

    // MLC-252 A-1: вычисляет осмысленный ключ группы.
    //   • непустой Context → (Context, isUnspecified=false);
    //   • иначе осмысленный метод (не числовой, не в денлисте) → (method, false);
    //   • иначе → (UnspecifiedKey, true) — единая группа «контекст не указан».
    private static (string Key, bool IsUnspecified) ResolveGroupKey(string? context, string? method)
    {
        if (!string.IsNullOrWhiteSpace(context))
        {
            return (context, false);
        }

        if (!string.IsNullOrWhiteSpace(method)
            && !NumericTokenRegex().IsMatch(method)
            && !ServiceTokenDenylist.Contains(method))
        {
            return (method, false);
        }

        return (UnspecifiedKey, true);
    }

    // Стабильный ключ единой группы «контекст не указан» (MLC-252 A-1). FE локализует по IsUnspecified,
    // но ключ нужен непустой и уникальный (StringComparer.Ordinal у словаря групп). Берём явный маркер.
    private const string UnspecifiedKey = "(контекст не указан)";

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
    private sealed class GroupAccumulator(string context, bool isUnspecified)
    {
        public string Context { get; } = context;
        public bool IsUnspecified { get; } = isUnspecified;
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
