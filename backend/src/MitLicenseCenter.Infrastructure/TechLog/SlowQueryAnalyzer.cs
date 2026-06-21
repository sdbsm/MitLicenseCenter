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
                // Длительность обязана быть — без неё в топ не попадаем.
                var durationMicros = ev.DurationMicroseconds;
                if (durationMicros is null)
                {
                    skipped++;
                    continue;
                }

                // Порог: 40_TECHLOG §6 — фильтр Dur в logcfg не работает, делаем здесь.
                if (durationMicros.Value < thresholdMicroseconds)
                {
                    continue;
                }

                var rawProcessName = ev.First("p:processName");
                var entry = new SlowQueryEntry
                {
                    Ts = ev.Ts,
                    DurationMicroseconds = durationMicros.Value,
                    DurationSeconds = ev.DurationSeconds ?? durationMicros.Value / 1_000_000d,
                    Sql = ev.First("Sql"),
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

        // Топ-N по длительности убывающим.
        var topQueries = aboveThreshold
            .OrderByDescending(e => e.DurationMicroseconds)
            .Take(topN)
            .ToList();

        // Группировка похожих: только записи с непустым Sql.
        // Запись без Sql в SimilarGroups не входит (нечего нормализовать, §7).
        var similarGroups = aboveThreshold
            .Where(e => !string.IsNullOrWhiteSpace(e.Sql))
            .GroupBy(e => NormalizeSql(e.Sql!))
            .Select(g => new SlowQueryGroup
            {
                NormalizedSql = g.Key,
                Count = g.Count(),
                TotalDurationMicroseconds = g.Sum(e => e.DurationMicroseconds),
                MaxDurationMicroseconds = g.Max(e => e.DurationMicroseconds),
                TotalDurationSeconds = g.Sum(e => e.DurationSeconds),
                MaxDurationSeconds = g.Max(e => e.DurationSeconds),
            })
            .OrderByDescending(g => g.TotalDurationMicroseconds)
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
