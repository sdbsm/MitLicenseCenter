namespace MitLicenseCenter.Application.Maintenance;

// Снимок планов обслуживания SQL раздела «Обслуживание» (MLC-217, ADR-54): live-read
// msdb.dbo.sysmaintplan_* + история заданий SQL Agent — БЕЗ собственных таблиц/миграций/джоб
// (только чтение). Деградация — статусом MaintenancePlansStatus (как BackupFreshnessSnapshot):
//   Ok           — прочитали планы/под-планы (Plans может быть пуст — планов нет);
//   AgentUnavailable — SQL Agent отсутствует/остановлен (Express-редакция) или нет прав на
//                  историю заданий — честный сигнал «агент недоступен», а не ошибка;
//   PermissionDenied — нет прав на msdb.dbo.sysmaintplan_* (история планов);
//   Unavailable  — SQL недоступен / строка подключения не настроена.
// В degraded-ветках Plans пуст.
public sealed record MaintenancePlansSnapshot(
    MaintenancePlansStatus Status,
    IReadOnlyList<MaintenancePlan> Plans);

// План обслуживания (sysmaintplan_plans) с его под-планами (sysmaintplan_subplans).
public sealed record MaintenancePlan(
    string Name,
    IReadOnlyList<MaintenanceSubplan> Subplans);

// Под-план (sysmaintplan_subplans) — привязан к заданию SQL Agent по job_id. Один под-план =
// одно расписание/одно задание. LastRun — последний прогон связанного задания (история
// sysjobhistory/sysjobactivity). HasSchedule — есть ВКЛЮЧЁННОЕ расписание у задания (а не
// ручной запуск). Tasks — детализация по шагам последнего прогона из sysmaintplan_log/
// _logdetail (что именно упало). LastRun/Tasks могут быть пусты (задание не запускалось).
public sealed record MaintenanceSubplan(
    string Name,
    bool HasSchedule,
    MaintenanceRunOutcome Outcome,
    DateTime? LastRunUtc,
    double? DurationSeconds,
    IReadOnlyList<MaintenanceTaskDetail> Tasks);

// Один шаг последнего прогона под-плана (sysmaintplan_logdetail): что именно делалось
// (проверка целостности / бэкап / реиндекс / …) и чем закончилось.
public sealed record MaintenanceTaskDetail(
    string Detail,
    bool Succeeded);

// Итог последнего прогона под-плана. Классификация — чистая SubplanRunPolicy:
//   Succeeded   — последний прогон успешен;
//   Failed      — последний прогон провалился (или часть шагов упала);
//   Overdue     — под-план С расписанием НЕ запускался / запускался давно (просрочен);
//   NeverRun    — под-план БЕЗ расписания (ручной) и не запускался — это норма, не алерт;
//   Unknown     — данных истории нет, классифицировать нельзя.
public enum MaintenanceRunOutcome
{
    Succeeded,
    Failed,
    Overdue,
    NeverRun,
    Unknown,
}

// Статус пробы планов обслуживания. Строкой на проводе (как MaintenanceProbeStatus) —
// forward-compat границы FE.
public enum MaintenancePlansStatus
{
    Ok,
    AgentUnavailable,
    PermissionDenied,
    Unavailable,
}
