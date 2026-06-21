using System.Globalization;
using System.Text.RegularExpressions;
using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Анализатор долгих запросов к СУБД (MLC-234, этап B трека «Расследование производительности»).
// Реализует ISlowQueryAnalyzer: потребляет IEnumerable<TechLogEvent>, строит SlowQueryAnalysisResult.
// internal sealed — зеркаль LockTreeAnalyzer (тот же слой и стиль).
//
// ГРАНИЦА (40_TECHLOG §5): ТОЛЬКО события DBMSSQL.
// Все остальные события (TLOCK, SDBL, EXCP, CALL…) молча игнорируются.
//
// Ключевой факт стенда (40_TECHLOG §6): фильтр по длительности в logcfg НЕ работает
// для JSON-ТЖ 8.5 — порог применяет ЭТОТ анализатор (параметр Analyze).
//
// Устойчивость (40_TECHLOG §7, принцип never-throws):
//   • «поля-призраки»: Sql у DBMSSQL иногда отсутствует (вторая запись dbmssql.ndjson);
//     Context, Rows, RowsAffected, SessionID, Usr — набор полей варьируется;
//   • дубли ключей берём через First() TechLogEvent — не падаем;
//   • p:processName нормализуется через TechLogProcessName.Normalize (общий хелпер MLC-234);
//   • запись без Sql попадает в TopQueries (Sql=null), но в SimilarGroups не идёт
//     (нечего нормализовать);
//   • любое нераспознанное/неполное — пропускаем, не бросаем.
//
// Агрегат «много мелких» (MLC-248): TopQueries гейтит по thresholdMicroseconds (единичные тяжёлые),
// а SimilarGroups строится по ВСЕМ DBMSSQL с непустым Sql (НЕЗАВИСИМО от порога) — типовая медленность
// «закрытия месяца» (тысячи быстрых запросов одной формы) всплывает суммарным временем, даже когда ни
// один запрос не длиннее порога. Сортировка групп по TotalDurationMicroseconds убыв., лимит topN.
//
// Корреляция SQL↔CALL (MLC-251): у событий DBMSSQL фоновых заданий часто НЕТ поля Context («какой код 1С
// выдал этот SQL»), а у событий CALL — есть. Событие ТЖ логируется в МОМЕНТ ОКОНЧАНИЯ, поэтому окно вызова
// CALL = [ts − duration, ts]; вложенные DBMSSQL того же t:connectID лежат внутри окна. Анализатор связывает
// каждый долгий запрос с породившим CALL по connectID + охватывающему окну и кладёт типовой (самый частый)
// контекст в SlowQueryGroup.SampleContext. Метод ПЕРЕЧИСЛЯЕТ events ДВАЖДЫ: проход 1 — индекс CALL по
// connectID; проход 2 — существующий цикл по DBMSSQL (атрибуция on-the-fly). Конвейер материализует события
// в List (TechLogCollectionService.RunAnalysisPipelineAsync → .ToList()), поэтому повторная итерация дешева;
// CALL-событий кратно меньше DBMSSQL, индекс — в памяти. База/ИБ группы берутся прямо из DBMSSQL (без CALL).
internal sealed partial class SlowQueryAnalyzer : ISlowQueryAnalyzer
{
    // Нормализация SQL: схлопываем строковые литералы ('...'), числовые литералы (123, 1.5)
    // и именованные/позиционные параметры (@P1, @param, ?) к плейсхолдеру «?».
    // Простая нормализация регулярками (не полноценный SQL-парсер): цель — сгруппировать
    // структурно одинаковые запросы с разными значениями фильтров/параметров.
    // Порядок важен: сначала строки (могут содержать цифры), потом числа и параметры.
    [GeneratedRegex(@"'[^']*'", RegexOptions.CultureInvariant)]
    private static partial Regex StringLiteralRegex();

    [GeneratedRegex(@"@\w+|\?(?!\?)", RegexOptions.CultureInvariant)]
    private static partial Regex NamedParamRegex();

    [GeneratedRegex(@"\b\d+(\.\d+)?\b", RegexOptions.CultureInvariant)]
    private static partial Regex NumericLiteralRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    // Поле плана запроса (best-effort): собирается ТОЛЬКО при явном теге <plansql/>
    // в logcfg (40_TECHLOG §5/§6). По умолчанию НЕ собирается.
    // Имя поля — по ОФИЦ. спеке §7 (41_LOGCFG_SPEC): «Сам план попадает в свойство planSQLText»
    // (строчная p, SQL заглавными). Регистр критичен: TechLogEvent.First сравнивает ключи через
    // StringComparison.Ordinal (MLC-247 B4), и при неверном регистре план НИКОГДА не прочитался бы.
    // Спека авторитетна; точную форму на стенде 8.5 ещё стоит подтвердить вживую, но имя берём из спеки.
    private const string PlanField = "planSQLText";

    public SlowQueryAnalysisResult Analyze(
        IEnumerable<TechLogEvent> events,
        long thresholdMicroseconds = 1_000_000,
        int topN = 50)
    {
        ArgumentNullException.ThrowIfNull(events);

        // Проход 1 (MLC-251): индекс окон CALL по t:connectID. Перечисляет events первый раз — список из
        // конвейера итерируется дважды (см. комментарий класса). Для каждого CALL с ts+duration+непустым
        // Context: окно [ts−duration, ts]. ts парсим толерантно (не распарсилось → CALL пропускаем).
        var callIndex = BuildCallIndex(events);

        var aboveThreshold = new List<SlowQueryEntry>();
        var totalDbmssql = 0;
        var skipped = 0;

        // Аккумулятор групп — по ВСЕМ DBMSSQL с непустым Sql (НЕ только прошедшим порог): «много мелких в
        // сумме» (MLC-248). Держим лёгкое состояние на форму запроса (счётчик + суммы), стримово, без
        // материализации каждого под-порогового события — на больших журналах (1+ ГБ) это важно.
        var groups = new Dictionary<string, GroupAccumulator>(StringComparer.Ordinal);

        foreach (var ev in events)
        {
            // Только DBMSSQL — всё остальное игнорируем (TLOCK, SDBL, EXCP, CALL…).
            if (!string.Equals(ev.Name, "DBMSSQL", StringComparison.Ordinal))
            {
                continue;
            }

            totalDbmssql++;

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
                var sql = ev.First("Sql");
                var rawProcessName = ev.First("p:processName");
                var database = ev.First("DataBase");
                var infobaseName = TechLogProcessName.Normalize(rawProcessName);

                // Агрегат «похожие запросы» — НЕЗАВИСИМ от порога: учитываем КАЖДОЕ событие с непустым Sql,
                // включая под-пороговые (тысячи быстрых запросов одной формы → группа с большим суммарным
                // временем). Запись без Sql в группировку не входит (нечего нормализовать, §7).
                if (!string.IsNullOrWhiteSpace(sql))
                {
                    var key = NormalizeSql(sql);
                    if (!groups.TryGetValue(key, out var acc))
                    {
                        acc = new GroupAccumulator(key);
                        groups[key] = acc;
                    }

                    acc.Add(durationMicros.Value, durationSeconds);

                    // MLC-251: обогащение группы. База/ИБ — прямо из DBMSSQL (корреляция не нужна).
                    acc.AddDatabase(database);
                    acc.AddInfobase(infobaseName);

                    // Контекст 1С — корреляция: ищем охватывающий CALL того же connectID по окну.
                    // Нет connectId/ts не парсится/нет совпадения → контекст не учитываем (null если ни одного).
                    var context = ResolveCallContext(callIndex, ev.First("t:connectID"), ev.Ts);
                    if (context is not null)
                    {
                        acc.AddContext(context);
                    }
                }

                // Порог: 40_TECHLOG §6 — фильтр Dur в logcfg не работает, делаем здесь.
                // Под-пороговые в TopQueries не идут (единичные тяжёлые), но уже учтены в агрегате выше.
                if (durationMicros.Value < thresholdMicroseconds)
                {
                    continue;
                }

                var entry = new SlowQueryEntry
                {
                    Ts = ev.Ts,
                    DurationMicroseconds = durationMicros.Value,
                    DurationSeconds = durationSeconds,
                    Sql = sql,
                    Context = ev.First("Context"),
                    DbPid = ev.First("dbpid"),
                    Rows = ev.First("Rows"),
                    RowsAffected = ev.First("RowsAffected"),
                    Database = database,
                    InfobaseName = infobaseName,
                    RawProcessName = rawProcessName,
                    SessionId = ev.First("SessionID"),
                    User = ev.First("Usr"),
                    // Имя поля плана — planSQLText по офиц. спеке §7 (регистр критичен для Ordinal-First).
                    PlanText = ev.First(PlanField),
                };

                aboveThreshold.Add(entry);
            }
            catch
            {
                // never-throws: любое непредвиденное исключение при разборе одного события
                // не ломает весь анализ — просто пропускаем (40_TECHLOG §7).
                skipped++;
            }
        }

        // Топ-N по длительности убывающим (единичные тяжёлые, гейт по порогу).
        var topQueries = aboveThreshold
            .OrderByDescending(e => e.DurationMicroseconds)
            .Take(topN)
            .ToList();

        // Группы похожих: по всем вхождениям формы (Count/Total/Max — по ВСЕМ событиям, не только пороговым).
        // Сортировка по суммарному времени убыв.; ограничение topN (раньше группы были без лимита).
        var similarGroups = groups.Values
            .OrderByDescending(g => g.TotalDurationMicroseconds)
            .Take(topN)
            .Select(g => new SlowQueryGroup
            {
                NormalizedSql = g.NormalizedSql,
                Count = g.Count,
                TotalDurationMicroseconds = g.TotalDurationMicroseconds,
                MaxDurationMicroseconds = g.MaxDurationMicroseconds,
                TotalDurationSeconds = g.TotalDurationSeconds,
                MaxDurationSeconds = g.MaxDurationSeconds,
                // MLC-251: типовой контекст (корреляция) + самые частые база/ИБ (из DBMSSQL).
                SampleContext = g.MostFrequentContext(),
                Database = g.MostFrequentDatabase(),
                InfobaseName = g.MostFrequentInfobase(),
            })
            .ToList();

        return new SlowQueryAnalysisResult
        {
            TopQueries = topQueries,
            SimilarGroups = similarGroups,
            TotalDbmssqlEvents = totalDbmssql,
            EventsAboveThreshold = aboveThreshold.Count,
            SkippedEvents = skipped,
        };
    }

    // ─── MLC-251: корреляция DBMSSQL↔CALL по t:connectID + временно́му окну ──────────────────────

    // Окно одного серверного вызова CALL: [Start, End] = [ts − duration, ts] + его Context.
    // ts событий ТЖ = момент ОКОНЧАНИЯ (40_TECHLOG), поэтому окно тянется назад на длительность.
    private readonly record struct CallWindow(DateTime Start, DateTime End, string Context);

    // Проход 1: индекс окон CALL по t:connectID. Берём только CALL с ts+duration+непустым Context
    // (контекст — единственное, ради чего связываем). ts парсим толерантно; не распарсилось/нет
    // duration/connectId/контекста → CALL пропускаем. Списки окон сортируем по Start — для бинарного
    // поиска охватывающего окна в проходе 2. Перечисляет events первый раз (повторная итерация — дёшево).
    private static Dictionary<string, List<CallWindow>> BuildCallIndex(IEnumerable<TechLogEvent> events)
    {
        var index = new Dictionary<string, List<CallWindow>>(StringComparer.Ordinal);

        foreach (var ev in events)
        {
            if (!string.Equals(ev.Name, "CALL", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var connectId = ev.First("t:connectID");
                if (string.IsNullOrWhiteSpace(connectId))
                {
                    continue;
                }

                var context = ev.First("Context");
                if (string.IsNullOrWhiteSpace(context))
                {
                    continue;
                }

                var durationMicros = ev.DurationMicroseconds;
                if (durationMicros is null)
                {
                    continue;
                }

                if (!TryParseTs(ev.Ts, out var end))
                {
                    continue;
                }

                // 1 микросекунда = 10 тиков (1 тик = 100 нс). Окно тянется назад от ts на длительность.
                var start = end.AddTicks(-durationMicros.Value * 10);

                if (!index.TryGetValue(connectId, out var windows))
                {
                    windows = new List<CallWindow>();
                    index[connectId] = windows;
                }

                windows.Add(new CallWindow(start, end, context));
            }
            catch
            {
                // never-throws: битое CALL-событие не ломает индексацию.
            }
        }

        foreach (var windows in index.Values)
        {
            windows.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        }

        return index;
    }

    // Проход 2 (атрибуция): по connectID запроса найти ОХВАТЫВАЮЩЕЕ окно CALL (Start ≤ tsЗапроса ≤ End);
    // при вложенности — самый ВНУТРЕННИЙ (последний по Start среди охватывающих). Возвращает Context CALL
    // или null. Пустой connectId/ts не парсится/нет совпадения → null (контекст не учитываем).
    // Поиск: бинарно по Start (верхняя граница Start ≤ ts), затем шаг назад с проверкой End ≥ ts —
    // первое окно с End ≥ ts при движении к меньшим Start и есть самое внутреннее охватывающее.
    private static string? ResolveCallContext(
        Dictionary<string, List<CallWindow>> callIndex,
        string? connectId,
        string? rawTs)
    {
        if (string.IsNullOrWhiteSpace(connectId)
            || !callIndex.TryGetValue(connectId, out var windows)
            || windows.Count == 0
            || !TryParseTs(rawTs, out var ts))
        {
            return null;
        }

        // Верхняя граница: индекс последнего окна со Start ≤ ts (бинарный поиск по отсортированному Start).
        var lo = 0;
        var hi = windows.Count - 1;
        var upper = -1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (windows[mid].Start <= ts)
            {
                upper = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        // Идём назад от upper: первое окно с End ≥ ts — самое внутреннее (наибольший Start) из охватывающих.
        for (var i = upper; i >= 0; i--)
        {
            if (windows[i].End >= ts)
            {
                return windows[i].Context;
            }
        }

        return null;
    }

    // Толерантный разбор ts ТЖ: «2026-06-21T23:07:18.573002» (микросекунды, 6 знаков), InvariantCulture.
    // Не распарсилось/пусто → false (вызывающий пропускает корреляцию). Не бросает.
    private static bool TryParseTs(string? raw, out DateTime ts)
    {
        if (!string.IsNullOrWhiteSpace(raw)
            && DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault,
                out ts))
        {
            return true;
        }

        ts = default;
        return false;
    }

    // Лёгкий аккумулятор группы похожих запросов (MLC-248): копит счётчик и суммы по форме запроса
    // стримово, не держа отдельные события под порогом в памяти (важно на журналах в гигабайты).
    private sealed class GroupAccumulator(string normalizedSql)
    {
        public string NormalizedSql { get; } = normalizedSql;
        public int Count { get; private set; }
        public long TotalDurationMicroseconds { get; private set; }
        public long MaxDurationMicroseconds { get; private set; }
        public double TotalDurationSeconds { get; private set; }
        public double MaxDurationSeconds { get; private set; }

        // MLC-251: гистограммы для выбора самого частого значения. Ленивые (создаются при первом
        // непустом значении) — у большинства групп контекста может не быть (нет CALL), не платим за пустой словарь.
        private Dictionary<string, int>? _contexts;
        private Dictionary<string, int>? _databases;
        private Dictionary<string, int>? _infobases;

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

        // MLC-251: накопление гистограмм (типовой контекст из корреляции, самые частые база/ИБ из DBMSSQL).
        public void AddContext(string? context) => Bump(ref _contexts, context);

        public void AddDatabase(string? database) => Bump(ref _databases, database);

        public void AddInfobase(string? infobase) => Bump(ref _infobases, infobase);

        public string? MostFrequentContext() => Argmax(_contexts);

        public string? MostFrequentDatabase() => Argmax(_databases);

        public string? MostFrequentInfobase() => Argmax(_infobases);

        private static void Bump(ref Dictionary<string, int>? hist, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            hist ??= new Dictionary<string, int>(StringComparer.Ordinal);
            hist[value] = hist.TryGetValue(value, out var c) ? c + 1 : 1;
        }

        // Ключ с максимальным счётчиком; при равенстве — стабильно (первый по порядку перечисления словаря,
        // достаточно для «типового» значения). null — если гистограмма пуста (ни одного непустого значения).
        private static string? Argmax(Dictionary<string, int>? hist)
        {
            if (hist is null || hist.Count == 0)
            {
                return null;
            }

            string? best = null;
            var bestCount = -1;
            foreach (var (key, count) in hist)
            {
                if (count > bestCount)
                {
                    best = key;
                    bestCount = count;
                }
            }

            return best;
        }
    }

    // Нормализация текста SQL к «шаблону» для группировки похожих запросов.
    // Алгоритм (простой, не SQL-парсер):
    //   1. Строковые литералы ('значение') → '?'
    //   2. Именованные (@P1, @param) и позиционные (?) параметры → ?
    //   3. Числовые литералы (123, 1.5) → ?
    //   4. Лишние пробелы → один пробел; trim.
    // Цель — схлопнуть «SELECT ... WHERE Id = 1» и «SELECT ... WHERE Id = 42» в одну группу.
    internal static string NormalizeSql(string sql)
    {
        // Строковые литералы — первыми (могут содержать цифры внутри).
        var result = StringLiteralRegex().Replace(sql, "'?'");
        // Именованные и позиционные параметры.
        result = NamedParamRegex().Replace(result, "?");
        // Числовые литералы (целые и дробные).
        result = NumericLiteralRegex().Replace(result, "?");
        // Нормализация пробелов.
        result = WhitespaceRegex().Replace(result, " ").Trim();
        return result;
    }
}
