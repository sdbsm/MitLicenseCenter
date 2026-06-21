namespace MitLicenseCenter.Domain.TechLog;

// Тип результата анализатора ТЖ (MLC-237, этап C). Один Finding на результат анализатора этапа B;
// Kind различает, ЧТО разбирали, а ResultJson несёт сам версионированный результат (см. Finding).
// Соответствует анализаторам этапа B: LockTreeAnalyzer (ManagedLocks), SlowQueryAnalyzer (SlowQueries),
// ExceptionAnalyzer (Exceptions), DbmsLockAnalyzer (DbmsLocks).
//
// Целочисленные значения ЗАМОРОЖЕНЫ — контракт с БД (HasConversion<int>), как InvestigationStatus.
// Новые члены добавляются только в конец с явным числом; существующие не переназначаются. Фактическое
// наполнение Finding — оркестрация MLC-238 (здесь объявление + хранилище).
public enum FindingKind
{
    // Управляемые блокировки 1С: TLOCK/TTIMEOUT/TDEADLOCK (LockTreeAnalyzer, MLC-233).
    ManagedLocks = 0,

    // Долгие запросы к СУБД: DBMSSQL (SlowQueryAnalyzer, MLC-234).
    SlowQueries = 1,

    // Исключения платформы 1С: EXCP (ExceptionAnalyzer, MLC-235).
    Exceptions = 2,

    // СУБД-блокировки: DBMSSQL с полями lkX (DbmsLockAnalyzer, MLC-236).
    DbmsLocks = 3,
}
