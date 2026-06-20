namespace MitLicenseCenter.Application.TechLog;

// Анализатор управляемых блокировок 1С (MLC-233, этап B трека «Расследование производительности»).
// Строит дерево «кто кого ждёт» и списки таймаутов/дедлоков из событий TLOCK/TTIMEOUT/TDEADLOCK.
//
// ГРАНИЦА: ТОЛЬКО управляемые блокировки уровня 1С (менеджер блокировок платформы).
// Блокировки уровня СУБД НЕ видны в этих событиях — их источник отдельный тег <dbmslocks/>
// с полями lkX (MLC-236). Не обещать единое дерево всех блокировок (40_TECHLOG §5).
//
// Архитектура: чистый C# без файловой системы и БД (как ITechLogParser/ILogcfgBuilder).
// Потребляет IEnumerable<TechLogEvent> — не знает про файлы/JSON/диск. Stateless → singleton.
// Устойчивость (40_TECHLOG §7): «поля-призраки», варианты имён (Usr/UserName), пустой
// WaitConnections, дубли ключей — анализатор НИКОГДА не бросает на неполном наборе полей.
public interface ILockTreeAnalyzer
{
    // Анализирует поток событий ТЖ: извлекает TLOCK-ожидания, TTIMEOUT, TDEADLOCK.
    // Не-блокировочные события (DBMSSQL, SDBL, EXCP и др.) молча игнорируются.
    // Не бросает исключений на любом входе (принцип never-throws слоя ТЖ).
    LockAnalysisResult Analyze(IEnumerable<TechLogEvent> events);
}
