namespace MitLicenseCenter.Application.Reporting;

// Готовый агрегат потребления одного тенанта за закрытый 15-минутный бакет
// (MLC-048, ADR-25). Выход аккумулятора; Infrastructure маппит его в сущность-
// телеметрию LicenseUsageSnapshot при персисте. Avg — double (running sum/count);
// Min/Max — int; Limit — последнее наблюдённое в бакете значение.
public sealed record LicenseUsageBucket(
    Guid TenantId,
    DateTime BucketStartUtc,
    int ConsumedMin,
    int ConsumedMax,
    double ConsumedAvg,
    int Limit);
