namespace MitLicenseCenter.Domain.TechLog;

// Жизненный цикл дела сбора ТЖ (MLC-230, трек 1.2, ADR-57/58). Целочисленные значения ЗАМОРОЖЕНЫ —
// это контракт с БД (HasConversion<int>), та же дисциплина, что у InfobaseStatus / PerfRecordingStatus
// / AuditActionType. На wire значения уходят строкой через JsonStringEnumConverter (Program.cs).
// Новые члены добавляются только в конец с явным числом; существующие не переназначаются.
public enum TechLogCollectionStatus
{
    Active = 0,
    Stopped = 1,
    Interrupted = 2,
}
