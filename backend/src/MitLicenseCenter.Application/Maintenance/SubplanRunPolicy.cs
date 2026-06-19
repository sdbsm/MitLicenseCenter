namespace MitLicenseCenter.Application.Maintenance;

// Политика классификации прогона под-плана обслуживания (MLC-217, ADR-54). ЧИСТАЯ логика —
// тестируется без SQL (как BackupFreshnessPolicy). На вход — сырые факты прогона, на выход —
// MaintenanceRunOutcome. Разделяет «по расписанию» и «по запросу»: под-план БЕЗ включённого
// расписания НЕ помечается «просрочен» (ручные под-планы владельца «перестроение индекса»/
// «month» — это норма, а не проблема). «Просрочен» (Overdue) — только под-план С расписанием,
// который давно/никогда не запускался.
public static class SubplanRunPolicy
{
    // Порог «просрочен» для под-плана С расписанием: ~26 часов (как BackupFreshnessPolicy —
    // суточный цикл + 2ч запас на длительность задания/сдвиг расписания). Фиксированная
    // константа, отдельной настройки не заводим (ADR-54). Задокументировано в docs/04_BACKEND.md.
    public static readonly TimeSpan ScheduledOverdueThreshold = TimeSpan.FromHours(26);

    // Классификация итога последнего прогона под-плана.
    //   нет истории  + есть расписание → Overdue (запланированный, но не запускался — алерт);
    //   нет истории  + нет расписания  → NeverRun (ручной под-план не запускался — норма);
    //   есть провал последнего прогона → Failed;
    //   успех + есть расписание + последний прогон старше порога → Overdue (отстал);
    //   успех (в пределах порога / без расписания) → Succeeded.
    // lastOutcomeSucceeded — null, если истории прогонов нет.
    public static MaintenanceRunOutcome Classify(
        bool hasSchedule,
        bool? lastOutcomeSucceeded,
        DateTime? lastRunUtc,
        DateTime nowUtc)
    {
        if (lastOutcomeSucceeded is null || lastRunUtc is null)
        {
            // Истории прогонов нет.
            return hasSchedule ? MaintenanceRunOutcome.Overdue : MaintenanceRunOutcome.NeverRun;
        }

        if (lastOutcomeSucceeded == false)
        {
            return MaintenanceRunOutcome.Failed;
        }

        // Последний прогон успешен. Для запланированного под-плана проверяем, не отстал ли он
        // (давно не запускался при действующем расписании).
        if (hasSchedule && nowUtc - lastRunUtc.Value > ScheduledOverdueThreshold)
        {
            return MaintenanceRunOutcome.Overdue;
        }

        return MaintenanceRunOutcome.Succeeded;
    }

    // Сводный сигнал «обслуживание требует внимания» по под-плану: провал ИЛИ просрочка
    // запланированного. NeverRun/Unknown/Succeeded — не алерт. Используется алертом на «Обзоре».
    public static bool IsAlerting(MaintenanceRunOutcome outcome) =>
        outcome is MaintenanceRunOutcome.Failed or MaintenanceRunOutcome.Overdue;
}
