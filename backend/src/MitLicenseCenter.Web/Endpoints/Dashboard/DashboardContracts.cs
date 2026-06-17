namespace MitLicenseCenter.Web.Endpoints;

public sealed record DashboardSummaryResponse(
    int TenantsTotal,
    int TenantsActive,
    int InfobasesTotal,
    int SessionsActiveTotal,
    int LicensesConsumedTotal,
    int LicensesAvailableTotal,
    // ADR-48 (MLC-166): false ⇒ факт rac --licenses недоступен; фронт показывает баннер
    // «данные о лицензиях недоступны» рядом с потреблением (счётчик отражает последний
    // факт, не ложный 0).
    bool LicenseFactAvailable,
    IReadOnlyList<TenantConsumptionRow> TopTenantsByConsumption,
    DashboardRasHealth Ras);

public sealed record TenantConsumptionRow(
    Guid TenantId,
    string TenantName,
    int Consumed,
    int Limit,
    int Percent);

// Stage 5 PR 5.1 (ADR-16): заменяет старый DashboardClusterStatus.
// LastCheckedAtUtc=null означает «первый ping ещё не отработал» — frontend
// рендерит «Проверка…» neutral badge.
public sealed record DashboardRasHealth(
    bool Healthy,
    DateTime? LastCheckedAtUtc,
    string? LastErrorMessage,
    int ConsecutiveFailures);

// MLC-186a — серверный агрегат сигналов «Требует внимания» для «Обзора». Один запрос
// (решение владельца — не N вызовов с фронта): нарушители квоты + дрейф панель↔кластер +
// мало места на диске бэкапов. RAS-здоровье и факт лицензий уже в /dashboard/summary — здесь
// НЕ дублируются (виджет берёт их из summary). Без аудита. Отдельный эндпоинт (не расширение
// /summary): источники тяжелее/медленнее (снапшот RAS через TTL-кэш 60с; server-side чтение
// свободного места SQL-диска) и каданс реже, чем у лёгкого 5-секундного summary.
public sealed record DashboardAlertsResponse(
    int QuotaWarning,
    int QuotaDanger,
    DashboardClusterDriftAlert? ClusterDrift,   // null ⇒ вызывающий не Admin (discovery — Admin-only)
    DashboardBackupDiskAlert BackupDisk);

// Дрейф панель↔кластер (MLC-092/095/150) — общий снапшот RAS (TTL-кэш 60с, без второго спавна
// rac). Available:false ⇒ RAS недоступен, счётчики неизвестны (null) — не «ложный ноль».
public sealed record DashboardClusterDriftAlert(
    bool Available,
    int? UnassignedBases,     // в кластере, не в панели, не скрытые
    int? BasesNotInCluster);  // в панели, нет в кластере

// Мало места на диске бэкапов (MLC-183 disk-guard). Configured:false ⇒ папка/сервер не заданы.
// FreeBytes=null при Configured ⇒ нет sysadmin / SQL недоступен / путь не локальный диск («не знаем»).
// Low = Configured && FreeBytes != null && FreeBytes < SafetyMarginBytes.
public sealed record DashboardBackupDiskAlert(
    bool Configured,
    long? FreeBytes,
    long SafetyMarginBytes,
    bool Low);
