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
}
