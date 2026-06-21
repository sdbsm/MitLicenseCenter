namespace MitLicenseCenter.Domain.TechLog;

// Жизненный цикл «Дела» расследования производительности (MLC-237, трек 1.2, этап C). Целочисленные
// значения ЗАМОРОЖЕНЫ — это контракт с БД (HasConversion<int>), та же дисциплина, что у InfobaseStatus /
// PerfRecordingStatus / AuditActionType. На wire значения уходят строкой через JsonStringEnumConverter
// (Program.cs). Новые члены добавляются только в конец с явным числом; существующие не переназначаются.
//
// История: заменяет TechLogCollectionStatus (Active=0/Stopped=1/Interrupted=2). При миграции данных
// старые значения отображаются на новые: Active(0)→Collecting(0), Stopped(1)→Completed(2),
// Interrupted(2)→Interrupted(3). Совместимость int с TechLogCollectionStatus НЕ требуется (отдельный
// контракт новой таблицы); маппинг выполняет миграция MLC237InvestigationModel.
//
// Семантика (50_DATA_MODEL): Collecting (идёт сбор ТЖ — установлен наш logcfg) → Analyzing (сбор снят,
// идёт разбор сырья в Finding — наполняет MLC-238) → Completed (отчёт готов). Плюс Interrupted (оборвано
// рестартом процесса/ОС) и Failed (сбор/разбор упал — наполняет MLC-238). На этапе C фактически
// используются Collecting/Completed/Interrupted (механический перенос поведения TechLogCollection);
// Analyzing/Failed объявлены для оркестрации MLC-238, чтобы не двигать frozen-int enum позже.
public enum InvestigationStatus
{
    // Идёт сбор ТЖ (наш logcfg.xml установлен в conf платформы). Эквивалент старого Active.
    Collecting = 0,

    // Сбор снят, идёт разбор сырья ТЖ в Finding (MLC-238). Промежуточное состояние оркестрации.
    Analyzing = 1,

    // Дело завершено: сбор снят, отчёт сформирован. Эквивалент старого Stopped.
    Completed = 2,

    // Дело оборвано рестартом процесса/ОС (in-memory стейт потерян). Эквивалент старого Interrupted.
    Interrupted = 3,

    // Сбор или разбор завершился ошибкой (MLC-238). Объявлено заранее (frozen-int дисциплина).
    Failed = 4,
}
