namespace MitLicenseCenter.Domain.TechLog;

// Сценарий целевого сбора ТЖ «Дела» (MLC-237, этап C; 40_TECHLOG §6). Domain-зеркало Application-enum
// TechLogScenario: int-значения СОВПАДАЮТ один-в-один (Locks=0..DbmsLocks=4), поэтому маппинг между
// слоями — прямой каст. Отдельный Domain-тип нужен потому, что Domain НЕ зависит от Application (см.
// MitLicenseCenter.Domain.csproj — без project references), а у TechLogCollection Scenario хранился
// строкой именно по этой причине. Здесь храним enum'ом int (HasConversion<int>) — спека 50_DATA_MODEL
// требует Scenario как enum.
//
// Целочисленные значения ЗАМОРОЖЕНЫ — контракт с БД (HasConversion<int>) И совместимость с
// TechLogScenario (миграция данных переносит строку→int по имени). Новые члены — только в конец с явным
// числом; существующие не переназначаются. Имена и семантика — 1:1 с TechLogScenario (см. его XML-доку).
public enum InvestigationScenario
{
    // Управляемые блокировки уровня 1С: TLOCK/TTIMEOUT/TDEADLOCK + SDBL.
    Locks = 0,

    // Долгие запросы к СУБД + планы: DBMSSQL/SDBL + <plansql/>.
    SlowQueries = 1,

    // Исключения/падения платформы: EXCP/EXCPCNTX + <dump/>.
    Exceptions = 2,

    // Общая медленная серверная работа: CALL/DBMSSQL.
    GeneralSlow = 3,

    // Блокировки уровня СУБД: DBMSSQL + config-level <dbmslocks/> (поля lkX).
    DbmsLocks = 4,
}
