namespace MitLicenseCenter.Application.Maintenance;

// Политика свежести бэкапов (MLC-216, ADR-54). Чистая логика расчёта флага «устарел» —
// тестируется без SQL. Порог свежести — ФИКСИРОВАННАЯ константа (отдельной настройки НЕ
// заводим, ADR-54): полный (FULL) бэкап старше порога ⇒ база «устарела». ~26 часов — суточный
// цикл FULL-бэкапа плюс запас ~2 часа на длительность задания / сдвиг расписания, чтобы
// штатный ночной бэкап не моргал «устарел» на границе суток.
public static class BackupFreshnessPolicy
{
    // Порог свежести FULL-бэкапа: ~26 часов (сутки + 2ч запас). Задокументировано в docs/04_BACKEND.md.
    public static readonly TimeSpan FullFreshnessThreshold = TimeSpan.FromHours(26);

    // «Устарел», если нет ни одного FULL-бэкапа ЛИБО последний FULL старше порога относительно
    // nowUtc. DIFF/LOG на флаг не влияют (база без свежего FULL не восстанавливаема одними
    // дифф/лог-бэкапами) — они показываются в таблице как доп. информация.
    public static bool IsStale(DateTime? lastFullUtc, DateTime nowUtc) =>
        lastFullUtc is not { } full || nowUtc - full > FullFreshnessThreshold;
}
