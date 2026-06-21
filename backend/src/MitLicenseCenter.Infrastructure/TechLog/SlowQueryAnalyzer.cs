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
                }

                // Порог: 40_TECHLOG §6 — фильтр Dur в logcfg не работает, делаем здесь.
                // Под-пороговые в TopQueries не идут (единичные тяжёлые), но уже учтены в агрегате выше.
                if (durationMicros.Value < thresholdMicroseconds)
                {
                    continue;
                }

                var rawProcessName = ev.First("p:processName");
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
                    Database = ev.First("DataBase"),
                    InfobaseName = TechLogProcessName.Normalize(rawProcessName),
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
