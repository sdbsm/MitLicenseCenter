namespace MitLicenseCenter.Web.Endpoints;

// MLC-049: ответ обоих эндпоинтов /reports/license-usage (сводка и drill-down) —
// единая форма, чтобы FE рисовал тем же компонентом график. FromUtc/ToUtc —
// эффективный диапазон после дефолта/клампа (см. ReportsEndpoints.ResolveRange).
public sealed record LicenseUsageSeriesResponse(
    IReadOnlyList<LicenseUsageBucketPoint> Buckets,
    DateTime FromUtc,
    DateTime ToUtc,
    int PeakConsumed,
    int PeakLimit,
    DateTime? PeakAtUtc,
    double AverageConsumed);

// Точка ряда. В drill-down — хранимые значения одного тенанта как есть; в сводке —
// суммы по тенантам в бакете (ConsumedMax/ConsumedAvg/Limit = Σ по тенантам бакета).
public sealed record LicenseUsageBucketPoint(
    DateTime BucketStartUtc,
    double ConsumedAvg,
    int ConsumedMax,
    int Limit);
