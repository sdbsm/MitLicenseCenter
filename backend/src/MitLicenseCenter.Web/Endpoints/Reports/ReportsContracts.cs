namespace MitLicenseCenter.Web.Endpoints;

// MLC-049: ответ обоих эндпоинтов /reports/license-usage (сводка и drill-down) —
// единая форма, чтобы FE рисовал тем же компонентом график. FromUtc/ToUtc —
// эффективный диапазон после дефолта/клампа (см. ReportsEndpoints.ResolveRange).
// Clamped=true, когда запрошенная ширина превысила MaxSpanDays и сервер сдвинул from
// вперёд (MLC-054) — FE показывает плашку обрезки; пустой ряд тоже несёт флаги.
public sealed record LicenseUsageSeriesResponse(
    IReadOnlyList<LicenseUsageBucketPoint> Buckets,
    DateTime FromUtc,
    DateTime ToUtc,
    int PeakConsumed,
    int PeakLimit,
    DateTime? PeakAtUtc,
    double AverageConsumed,
    bool Clamped,
    int MaxSpanDays);

// Точка ряда. В drill-down — хранимые значения одного тенанта как есть; в сводке —
// суммы по тенантам в бакете (ConsumedMax/ConsumedAvg/Limit = Σ по тенантам бакета).
public sealed record LicenseUsageBucketPoint(
    DateTime BucketStartUtc,
    double ConsumedAvg,
    int ConsumedMax,
    int Limit);
