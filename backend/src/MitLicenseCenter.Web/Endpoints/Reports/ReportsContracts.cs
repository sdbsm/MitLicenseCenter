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

// MLC-185e (ADR-25): read-API поверх dbo.DatabaseSizeSnapshots — зеркало
// /reports/license-usage. Сводка по хосту: Points — ряд «итог по хосту» во времени
// (Σ по всем базам инфобаз на момент снимка), Tenants — разбивка по клиентам на
// ПОСЛЕДНИЙ снимок периода (для таблицы). FromUtc/ToUtc/Clamped/MaxSpanDays — как у
// лицензий (см. ReportsEndpoints.ResolveRange). Пустой период → пустые ряды + честные
// From/To. Зеркалит LicenseUsageSeriesResponse.
public sealed record DatabaseSizeSeriesResponse(
    IReadOnlyList<DatabaseSizePoint> Points,
    IReadOnlyList<DatabaseSizeTenantRow> Tenants,
    DateTime FromUtc,
    DateTime ToUtc,
    bool Clamped,
    int MaxSpanDays);

// Точка ряда размера во времени. В сводке — Σ по всем базам инфобаз на момент снимка
// (итог по хосту); в drill-down — Σ по базам одного клиента на момент снимка.
// TotalBytes = DataBytes + LogBytes (вычисляемое, в сущности не хранится).
public sealed record DatabaseSizePoint(
    DateTime AtUtc,
    long DataBytes,
    long LogBytes,
    long TotalBytes);

// Строка разбивки по клиенту на последний снимок периода (таблица сводки).
// TenantName=null для осиротевших снимков (TenantId=null после удаления тенанта,
// FK SetNull) — поле опускается JSON-policy WhenWritingNull (ADR-32), FE рисует
// «без клиента». DatabaseCount — число баз клиента в этом снимке.
public sealed record DatabaseSizeTenantRow(
    Guid? TenantId,
    string? TenantName,
    long DataBytes,
    long LogBytes,
    long TotalBytes,
    int DatabaseCount);

// Drill-down одного клиента: ряд во времени по его базам (Points) + разбивка по
// базам на последний снимок периода (Databases). Зеркало DrilldownAsync.
// Несуществующий клиент / нет данных → пустые ряды + честные From/To.
public sealed record DatabaseSizeTenantSeriesResponse(
    IReadOnlyList<DatabaseSizePoint> Points,
    IReadOnlyList<DatabaseSizeDatabaseRow> Databases,
    DateTime FromUtc,
    DateTime ToUtc,
    bool Clamped,
    int MaxSpanDays);

// Строка разбивки по базе клиента на последний снимок периода (таблица drill-down).
public sealed record DatabaseSizeDatabaseRow(
    string DatabaseName,
    long DataBytes,
    long LogBytes,
    long TotalBytes);
