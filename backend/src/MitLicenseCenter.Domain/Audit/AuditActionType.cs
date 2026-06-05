namespace MitLicenseCenter.Domain.Audit;

// Целочисленные значения зафиксированы — это контракт с БД (HasConversion<int>) и c
// frontend (JsonStringEnumConverter сериализует именем, но reverse-lookup по int
// допускается). Пропуски в нумерации зарезервированы под Stage 3 (см. plan).
public enum AuditActionType
{
    TenantCreated = 1,
    TenantUpdated = 2,
    TenantDeleted = 3,

    InfobaseCreated = 10,
    InfobaseUpdated = 11,
    InfobaseDeleted = 12,
    InfobaseReassigned = 13,

    PublicationCreated = 20,
    PublicationUpdated = 21,
    PublicationDeleted = 22,

    AdminLoggedIn = 100,
    AdminLoggedOut = 101,
    AdminPasswordChanged = 102,

    SessionKilled = 200,
    LimitChanged = 201,

    // Publication drift (PR 3.5). Stage MLC-045 (ADR-4 переписан, ADR-4.1 revoked):
    // drift-enforcement удалён — новые строки с этими значениями НЕ пишутся. Enum-записи
    // сохранены для рендера исторических AuditLog rows (frozen-int rule).
    PublicationDriftDetected = 210,
    PublicationReconciled = 211,

    // Публикация через webinst и смена платформы (MLC-045). Published — успешная
    // (пере)публикация через webinst.exe. PlatformChanged — правка пути к wsisapi.dll
    // в web.config под новую версию платформы (default.vrd не трогается).
    PublicationPublished = 212,
    PublicationPlatformChanged = 213,

    // 1С Cluster adapter circuit-breaker transitions (PR 3.2).
    // Stage 5 PR 5.1 (ADR-16): circuit breaker удалён — новые строки с этими
    // значениями НЕ пишутся. Enum-записи сохранены для рендера исторических
    // AuditLog rows (frozen-int rule, PR 2.2). Frontend i18n
    // audit.actions.ClusterAdapterCircuit{Opened,Closed} тоже сохранены.
    ClusterAdapterCircuitOpened = 300,
    ClusterAdapterCircuitClosed = 301,

    SettingChanged = 400,

    // System maintenance (PR 4.3, opens 500-серию).
    AuditLogsPurged = 500,
}
