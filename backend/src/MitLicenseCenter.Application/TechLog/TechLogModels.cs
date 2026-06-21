namespace MitLicenseCenter.Application.TechLog;

// Сценарий целевого сбора ТЖ (40_TECHLOG §6). Определяет набор событий в logcfg — «целевой, не
// полный» сбор (инвариант безопасности 60_SAFETY №1: панель никогда не ставит «полный ТЖ»). НЕ
// контракт с БД (хранится строкой Scenario в снимке дела) — но держим стабильные имена: они уходят
// в logcfg и в аудит. Объём режется типом события и `p:processName`, НЕ фильтром длительности
// (для JSON-ТЖ 8.5 он не работает — MLC-229, 40_TECHLOG §6/§8).
//
// Frozen-int enums жизненного цикла дела (TechLogCollectionStatus / TechLogCollectionStopReason) —
// в Domain (контракт с БД HasConversion<int>, рядом с сущностью TechLogCollection).
public enum TechLogScenario
{
    // Управляемые блокировки уровня 1С: TLOCK/TTIMEOUT/TDEADLOCK + SDBL (контекст транзакций).
    Locks = 0,

    // Долгие запросы к СУБД + планы: DBMSSQL/SDBL + тег <plansql/> (порог длительности — в C#-парсере).
    SlowQueries = 1,

    // Исключения/падения платформы: EXCP/EXCPCNTX + дампы аварий <dump/>.
    Exceptions = 2,

    // Общая медленная серверная работа: CALL/DBMSSQL (без «всё подряд»).
    GeneralSlow = 3,

    // Блокировки уровня СУБД: событие DBMSSQL + config-level тег <dbmslocks/> (включает сбор полей lkX:
    // lka/lkp/lkpid/lkaid/lksrc/lkpto/lkato — 40_TECHLOG §5, 41_LOGCFG_SPEC §8, источник infostart 1431026).
    // Сами поля lkX выводятся благодаря <property name="all"/> в <log> (MLC-246) — отдельный набор
    // <property> для них не нужен. ОТДЕЛЬНЫЙ механизм от управляемых блокировок 1С (TLOCK/TTIMEOUT/
    // TDEADLOCK → сценарий Locks); не путать, не обещать единое дерево всех блокировок.
    // ⚠ Структура полей lkX в JSON-ТЖ 8.5 собрана по документации (infostart 1431026 / 40_TECHLOG §5);
    //   точная форма и поведение тега — за стенд-приёмкой (приёмка владельца).
    // ⚠ Объём: dbmslocks собирается без отборов по длительности → файлы могут превышать 6 ГБ/час
    //   (источник 1431026); полагаемся на лимит места (MLC-231) и короткое окно сбора.
    // Frozen-int: значение 4 — новое, существующие 0..3 не переназначались.
    DbmsLocks = 4,
}
