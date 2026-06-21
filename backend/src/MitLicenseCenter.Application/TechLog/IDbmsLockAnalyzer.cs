namespace MitLicenseCenter.Application.TechLog;

// Анализатор СУБД-блокировок (MLC-236, этап B трека «Расследование производительности»).
// Строит дерево «жертва → источник» из событий DBMSSQL с полями lkX.
//
// ГРАНИЦА: ТОЛЬКО СУБД-уровень (тег <dbmslocks/> + поля lkX — отдельный механизм от
// управляемых блокировок 1С). Управляемые блокировки (TLOCK/TTIMEOUT/TDEADLOCK) —
// ILockTreeAnalyzer (MLC-233). Не обещать единое дерево (40_TECHLOG §5).
//
// Алгоритм (40_TECHLOG §5, infostart 1431026):
//   • жертва: событие DBMSSQL с lkp=1 (поток заблокирован СУБД);
//   • источник: событие DBMSSQL с lka=1 (поток удерживает блокировку СУБД);
//   • связка: жертва.lksrc → источник.connectID (t:connectID или connectID);
//   • если источник в окне не найден — ребро всё равно отдаётся (SourceMatched=false).
//
// ⚠ Структура полей lkX собрана по документации (infostart 1431026, 40_TECHLOG §5).
//   Точная форма в JSON-ТЖ 8.5 подлежит подтверждению на стенде (приёмка владельца).
//
// Архитектура: чистый C# без файловой системы и БД (как ITechLogParser/ILogcfgBuilder).
// Потребляет IEnumerable<TechLogEvent> — не знает про файлы/JSON/диск. Stateless → singleton.
// Устойчивость (40_TECHLOG §7): «поля-призраки» lkX (появляются только при блокировке),
// варианты имён connectID, дубли ключей — никогда не бросает на неполном наборе полей.
public interface IDbmsLockAnalyzer
{
    // Анализирует поток событий ТЖ: строит дерево СУБД-блокировок из DBMSSQL с полями lkX.
    // Не-DBMSSQL события и DBMSSQL без lkX молча игнорируются.
    // Не бросает исключений на любом входе (принцип never-throws слоя ТЖ).
    DbmsLockAnalysisResult Analyze(IEnumerable<TechLogEvent> events);
}
