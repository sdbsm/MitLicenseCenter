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

    PublicationCreated = 20,
    PublicationUpdated = 21,
    PublicationDeleted = 22,

    AdminLoggedIn = 100,
    AdminLoggedOut = 101,
    AdminPasswordChanged = 102,

    SessionKilled = 200,
    LimitChanged = 201,

    // 1С Cluster adapter circuit-breaker transitions (PR 3.2).
    ClusterAdapterCircuitOpened = 300,
    ClusterAdapterCircuitClosed = 301,

    SettingChanged = 400,
}
