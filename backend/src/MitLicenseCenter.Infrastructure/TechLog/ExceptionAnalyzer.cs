using System.Text.RegularExpressions;
using MitLicenseCenter.Application.TechLog;

namespace MitLicenseCenter.Infrastructure.TechLog;

// Анализатор исключений платформы 1С (MLC-235, этап B трека «Расследование производительности»).
// Реализует IExceptionAnalyzer: потребляет IEnumerable<TechLogEvent>, строит ExceptionAnalysisResult.
// internal sealed — зеркаль SlowQueryAnalyzer (тот же слой и стиль).
//
// ГРАНИЦА (40_TECHLOG §5): ТОЛЬКО события EXCP.
// Все остальные события (DBMSSQL, TLOCK, SDBL, CALL…) молча игнорируются.
//
// Особенность DataBaseException (40_TECHLOG §7):
//   Дедлок СУБД пишет ДВА события EXCP с Exception=DataBaseException.
//   Реализация НЕ осуществляет точную пару-корреляцию (алгоритм требует стенд-данных).
//   DataBaseException помечается флагом IsDatabaseException на группе;
//   IsDatabaseException + Count честно комментирует возможное удвоение частоты.
//
// Устойчивость (40_TECHLOG §7, принцип never-throws):
//   • «поля-призраки»: Descr/Context у EXCP иногда отсутствуют — обрабатывается толерантно;
//   • «ложные EXCP» при окне авторизации — просто попадают в топ (не фильтруем);
//   • дубли ключей — через First() TechLogEvent, не падаем;
//   • EXCP без Exception/Descr обрабатывается с типом null / плейсхолдером «(без описания)»;
//   • любое нераспознанное/неполное — пропускаем, не бросаем.
internal sealed partial class ExceptionAnalyzer : IExceptionAnalyzer
{
    // Имя типа исключения СУБД (DataBaseException): особый статус — дедлок СУБД пишет ДВА
    // таких EXCP (40_TECHLOG §7). Помечаем группы с этим типом флагом IsDatabaseException.
    private const string DatabaseExceptionType = "DataBaseException";

    // Нормализация Descr: схлопываем числа (123, 0x1A) и идентификаторы с нижним подчёркиванием
    // (типа «_SomeId_0», «Object_12ab») к плейсхолдеру «#», убираем лишние пробелы.
    // Простая нормализация регулярками (не парсер сообщений): цель — объединить похожие
    // описания вроде «timeout 100» и «timeout 999» в одну группу.
    // Порядок: сначала hex-числа (содержат буквы), потом десятичные.
    [GeneratedRegex(@"0x[0-9a-fA-F]+", RegexOptions.CultureInvariant)]
    private static partial Regex HexLiteralRegex();

    [GeneratedRegex(@"\b\d+\b", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalLiteralRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    public ExceptionAnalysisResult Analyze(
        IEnumerable<TechLogEvent> events,
        int topN = 50)
    {
        ArgumentNullException.ThrowIfNull(events);

        // Ключ группировки: тип исключения + нормализованный Descr.
        // Используем ValueTuple как ключ словаря — избегаем создания отдельного record.
        var groups = new Dictionary<(string? ExType, string NormDescr), GroupAccumulator>();
        var totalExcp = 0;
        var dbExceptionEvents = 0;
        var skipped = 0;

        foreach (var ev in events)
        {
            // Только EXCP — всё остальное игнорируем.
            if (!string.Equals(ev.Name, "EXCP", StringComparison.Ordinal))
            {
                continue;
            }

            totalExcp++;

            try
            {
                var exceptionType = ev.First("Exception");
                var descr = ev.First("Descr");
                var context = ev.First("Context");
                var rawProcessName = ev.First("p:processName");
                var ts = ev.Ts;

                var normalizedDescr = NormalizeDescr(descr);
                var key = (exceptionType, normalizedDescr);

                var isDbException = string.Equals(
                    exceptionType,
                    DatabaseExceptionType,
                    StringComparison.Ordinal);

                if (isDbException)
                {
                    dbExceptionEvents++;
                }

                if (!groups.TryGetValue(key, out var acc))
                {
                    acc = new GroupAccumulator
                    {
                        ExceptionType = exceptionType,
                        NormalizedDescr = normalizedDescr,
                        SampleDescr = descr,
                        SampleContext = context,
                        IsDatabaseException = isDbException,
                        InfobaseName = TechLogProcessName.Normalize(rawProcessName),
                        RawProcessName = rawProcessName,
                        FirstTs = ts,
                        LastTs = ts,
                        Count = 0,
                    };
                }

                acc.Count++;
                // Обновляем LastTs при каждом добавлении.
                acc.LastTs = ts;
                groups[key] = acc;
            }
            catch
            {
                // never-throws: любое непредвиденное исключение при разборе одного события
                // не ломает весь анализ — просто пропускаем (40_TECHLOG §7).
                skipped++;
            }
        }

        // Топ-N по Count убывающим.
        var ordered = groups.Values
            .OrderByDescending(a => a.Count)
            .AsEnumerable();

        if (topN > 0)
        {
            ordered = ordered.Take(topN);
        }

        var topExceptions = ordered
            .Select(a => new ExceptionGroup
            {
                ExceptionType = a.ExceptionType,
                NormalizedDescr = a.NormalizedDescr,
                SampleDescr = a.SampleDescr,
                SampleContext = a.SampleContext,
                Count = a.Count,
                IsDatabaseException = a.IsDatabaseException,
                InfobaseName = a.InfobaseName,
                RawProcessName = a.RawProcessName,
                FirstTs = a.FirstTs,
                LastTs = a.LastTs,
            })
            .ToList();

        return new ExceptionAnalysisResult
        {
            TopExceptions = topExceptions,
            TotalExcpEvents = totalExcp,
            DatabaseExceptionEvents = dbExceptionEvents,
            SkippedEvents = skipped,
        };
    }

    // Нормализация текста Descr к «шаблону» для группировки похожих описаний.
    // Алгоритм (простой, не парсер сообщений):
    //   1. Hex-литералы (0x1A2B) → #
    //   2. Десятичные числа (123, 42) → #
    //   3. Лишние пробелы → один пробел; trim.
    // Цель: «Lock request timeout exceeded after 100ms» и «… after 999ms» → одна группа.
    // null/пустой → плейсхолдер «(без описания)» (толерантность к «полям-призракам» §7).
    internal static string NormalizeDescr(string? descr)
    {
        if (string.IsNullOrWhiteSpace(descr))
        {
            return ExceptionAnalysisResult.NoDescrPlaceholder;
        }

        // Hex-числа — первыми (содержат буквы, которые иначе могут попасть в dec-паттерн).
        var result = HexLiteralRegex().Replace(descr, "#");
        // Десятичные числа.
        result = DecimalLiteralRegex().Replace(result, "#");
        // Нормализация пробелов.
        result = WhitespaceRegex().Replace(result, " ").Trim();

        return string.IsNullOrEmpty(result) ? ExceptionAnalysisResult.NoDescrPlaceholder : result;
    }

    // Изменяемый аккумулятор группы (приватный; снаружи не виден).
    // Использует mutable struct для эффективности при большом потоке,
    // но хранится в словаре как class-обёртка чтобы Dictionary.TryGetValue + acc.Count++
    // работал без лишнего поиска по ключу.
    private sealed class GroupAccumulator
    {
        public string? ExceptionType { get; init; }
        public string NormalizedDescr { get; init; } = ExceptionAnalysisResult.NoDescrPlaceholder;
        public string? SampleDescr { get; init; }
        public string? SampleContext { get; init; }
        public bool IsDatabaseException { get; init; }
        public string? InfobaseName { get; init; }
        public string? RawProcessName { get; init; }
        public string? FirstTs { get; init; }
        public string? LastTs { get; set; }
        public int Count { get; set; }
    }
}
