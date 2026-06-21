namespace MitLicenseCenter.Application.TechLog;

// Анализатор серверных вызовов 1С из событий CALL (MLC-249, трек «Расследование производительности»).
// Строит из потока TechLogEvent топ долгих вызовов и агрегат группировки по контексту.
//
// ГРАНИЦА: ТОЛЬКО события CALL. Другие события (DBMSSQL, TLOCK, EXCP и др.) игнорируются.
//
// Зачем отдельно от долгих запросов (ISlowQueryAnalyzer): DBMSSQL = время В СУБД, а CALL = серверные
// вызовы 1С (вычисления МЕЖДУ запросами). Сценарий GeneralSlow собирает CALL+DBMSSQL — «общая
// медленная серверная работа»; этот анализатор разбирает CALL-сторону.
//
// ⚠ У CALL НЕТ p:processName (40_TECHLOG §8) → привязки к арендатору по процессу нет (в отличие от
// SlowQueryAnalyzer) — InfobaseName не вычисляем. Группировка — по сырому Context (стек 1С).
//
// Архитектура: чистый C# без файловой системы и БД (как ISlowQueryAnalyzer/ITechLogParser).
// Stateless → singleton. Устойчивость (40_TECHLOG §7): «поля-призраки», дубли ключей, варианты
// имён — анализатор НИКОГДА не бросает на любом входе (принцип never-throws слоя ТЖ).
public interface ICallAnalyzer
{
    // Анализирует поток событий ТЖ: отбирает CALL с длительностью ≥ порога для топа,
    // строит агрегат групп по контексту по ВСЕМ CALL (независимо от порога).
    //
    // thresholdMicroseconds — порог длительности В МИКРОСЕКУНДАХ (единица ТЖ 8.5, 40_TECHLOG §4)
    //   для TopCalls. Вызовы короче порога в топ не попадают (но в агрегат — попадают).
    //   Дефолт 1 000 000 µs = 1 секунда.
    //
    // topN — максимальное число записей в TopCalls и SimilarGroups. Дефолт 50.
    //
    // Не бросает исключений на любом входе (принцип never-throws слоя ТЖ).
    CallAnalysisResult Analyze(
        IEnumerable<TechLogEvent> events,
        long thresholdMicroseconds = 1_000_000,
        int topN = 50);
}
