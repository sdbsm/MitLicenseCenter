using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-049 (ADR-25): read-API поверх собранной MLC-048 таблицы dbo.LicenseUsageSnapshots.
// Vertical slice (ADR-20) — прямая инъекция AppDbContext, без новых ADR (только чтение).
public static class ReportsEndpoints
{
    // Дефолтное окно ряда (оба from/to опущены) и верхний кламп ширины — берегут размер
    // payload графика (15-мин бакеты: 7 дней ≈ 672 точки, 31 день ≈ 2976).
    internal static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(7);
    internal static readonly TimeSpan MaxSpan = TimeSpan.FromDays(31);

    public static void MapReportsEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/reports")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Reports");

        group.MapGet("/license-usage", SummaryAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/license-usage/{tenantId:guid}", DrilldownAsync).RequireAuthorization(Roles.Viewer);

        group.MapGet("/database-size", DatabaseSizeSummaryAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/database-size/{tenantId:guid}", DatabaseSizeDrilldownAsync).RequireAuthorization(Roles.Viewer);
    }

    // Сводка по всем клиентам: ряд по бакетам, агрегированный по тенантам (на бакет:
    // ConsumedMax/ConsumedAvg/Limit = Σ по тенантам). Осиротевшие записи (TenantId=null
    // после удаления тенанта, SetNull) ВКЛЮЧАЮТСЯ — фильтра по TenantId нет: история
    // платформы не «усыхает» при удалении клиента (прецедент AuditLog, ADR-25).
    internal static async Task<Results<Ok<LicenseUsageSeriesResponse>, ValidationProblem>> SummaryAsync(
        AppDbContext db,
        TimeProvider clock,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (RangeError(from, to) is { } error)
        {
            return TypedResults.ValidationProblem(error);
        }

        var (rangeFrom, rangeTo, clamped) = ResolveRange(from, to, clock.GetUtcNow().UtcDateTime);

        var buckets = await db.LicenseUsageSnapshots
            .AsNoTracking()
            .Where(x => x.BucketStartUtc >= rangeFrom && x.BucketStartUtc <= rangeTo)
            .GroupBy(x => x.BucketStartUtc)
            .OrderBy(g => g.Key)
            .Select(g => new LicenseUsageBucketPoint(
                g.Key,
                g.Sum(x => x.ConsumedAvg),
                g.Sum(x => x.ConsumedMax),
                g.Sum(x => x.Limit)))
            .ToListAsync(ct).ConfigureAwait(false);

        return TypedResults.Ok(BuildResponse(buckets, rangeFrom, rangeTo, clamped, MaxSpan.Days));
    }

    // Drill-down одного тенанта: хранимые значения как есть. Null-тенант строки сюда не
    // попадают (guid != null). Несуществующий tenantId → пустой ряд (не 404).
    internal static async Task<Results<Ok<LicenseUsageSeriesResponse>, ValidationProblem>> DrilldownAsync(
        AppDbContext db,
        TimeProvider clock,
        Guid tenantId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (RangeError(from, to) is { } error)
        {
            return TypedResults.ValidationProblem(error);
        }

        var (rangeFrom, rangeTo, clamped) = ResolveRange(from, to, clock.GetUtcNow().UtcDateTime);

        var buckets = await db.LicenseUsageSnapshots
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BucketStartUtc >= rangeFrom && x.BucketStartUtc <= rangeTo)
            .OrderBy(x => x.BucketStartUtc)
            .Select(x => new LicenseUsageBucketPoint(
                x.BucketStartUtc,
                x.ConsumedAvg,
                x.ConsumedMax,
                x.Limit))
            .ToListAsync(ct).ConfigureAwait(false);

        return TypedResults.Ok(BuildResponse(buckets, rangeFrom, rangeTo, clamped, MaxSpan.Days));
    }

    // MLC-185e: сводка размера баз по хосту. Зеркало SummaryAsync лицензий.
    // Points — «итог по хосту» во времени: GroupBy(SnapshotAtUtc) по ВСЕМ снимкам баз
    // инфобаз в периоде, Σ Data/Log (осиротевшие TenantId=null включены — история не
    // усыхает при удалении клиента, как у лицензий). Tenants — разбивка по клиентам на
    // ПОСЛЕДНИЙ снимок периода (Max(SnapshotAtUtc) подзапросом, затем фильтр == max),
    // GroupBy(TenantId), имя клиента — left-join на Tenants. Осиротевшие (TenantId=null)
    // дают строку с TenantName=null (FE: «без клиента»). Период/кламп — ResolveRange.
    internal static async Task<Results<Ok<DatabaseSizeSeriesResponse>, ValidationProblem>> DatabaseSizeSummaryAsync(
        AppDbContext db,
        TimeProvider clock,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (RangeError(from, to) is { } error)
        {
            return TypedResults.ValidationProblem(error);
        }

        var (rangeFrom, rangeTo, clamped) = ResolveRange(from, to, clock.GetUtcNow().UtcDateTime);

        var inRange = db.DatabaseSizeSnapshots
            .AsNoTracking()
            .Where(x => x.SnapshotAtUtc >= rangeFrom && x.SnapshotAtUtc <= rangeTo);

        var points = await inRange
            .GroupBy(x => x.SnapshotAtUtc)
            .OrderBy(g => g.Key)
            .Select(g => new DatabaseSizePoint(
                g.Key,
                g.Sum(x => x.DataBytes),
                g.Sum(x => x.LogBytes),
                g.Sum(x => x.DataBytes) + g.Sum(x => x.LogBytes)))
            .ToListAsync(ct).ConfigureAwait(false);

        var tenants = await LastSnapshotTenantRowsAsync(db, inRange, ct).ConfigureAwait(false);

        return TypedResults.Ok(new DatabaseSizeSeriesResponse(
            points, tenants, rangeFrom, rangeTo, clamped, MaxSpan.Days));
    }

    // Разбивка по клиентам на последний снимок периода + left-join имён. Вынесено
    // отдельным методом, т.к. имя клиента — клиентский join (Tenants.Name) поверх
    // SQL-агрегации по TenantId; осиротевшие (TenantId=null) → TenantName=null.
    private static async Task<List<DatabaseSizeTenantRow>> LastSnapshotTenantRowsAsync(
        AppDbContext db, IQueryable<DatabaseSizeSnapshot> inRange, CancellationToken ct)
    {
        // Max(SnapshotAtUtc) в периоде. Нет снимков → null → пустая разбивка.
        var lastAt = await inRange
            .MaxAsync(x => (DateTime?)x.SnapshotAtUtc, ct).ConfigureAwait(false);
        if (lastAt is not { } last)
        {
            return [];
        }

        var grouped = await inRange
            .Where(x => x.SnapshotAtUtc == last)
            .GroupBy(x => x.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                DataBytes = g.Sum(x => x.DataBytes),
                LogBytes = g.Sum(x => x.LogBytes),
                DatabaseCount = g.Count(),
            })
            .ToListAsync(ct).ConfigureAwait(false);

        // Имена клиентов одним проходом (left-join в памяти: осиротевшие → null).
        var names = await db.Tenants
            .AsNoTracking()
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct).ConfigureAwait(false);

        return grouped
            .Select(g => new DatabaseSizeTenantRow(
                g.TenantId,
                g.TenantId is { } id && names.TryGetValue(id, out var name) ? name : null,
                g.DataBytes,
                g.LogBytes,
                g.DataBytes + g.LogBytes,
                g.DatabaseCount))
            .OrderByDescending(r => r.TotalBytes)
            .ThenBy(r => r.TenantName, StringComparer.Ordinal)
            .ToList();
    }

    // MLC-185e: drill-down одного клиента. Зеркало DrilldownAsync лицензий.
    // Points — ряд во времени по базам клиента (GroupBy(SnapshotAtUtc), TenantId==id).
    // Databases — разбивка по базам на последний снимок периода (для таблицы).
    // Null-тенант сюда не попадает (guid != null). Несуществующий клиент / нет данных →
    // пустые ряды + честные From/To.
    internal static async Task<Results<Ok<DatabaseSizeTenantSeriesResponse>, ValidationProblem>> DatabaseSizeDrilldownAsync(
        AppDbContext db,
        TimeProvider clock,
        Guid tenantId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (RangeError(from, to) is { } error)
        {
            return TypedResults.ValidationProblem(error);
        }

        var (rangeFrom, rangeTo, clamped) = ResolveRange(from, to, clock.GetUtcNow().UtcDateTime);

        var inRange = db.DatabaseSizeSnapshots
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.SnapshotAtUtc >= rangeFrom && x.SnapshotAtUtc <= rangeTo);

        var points = await inRange
            .GroupBy(x => x.SnapshotAtUtc)
            .OrderBy(g => g.Key)
            .Select(g => new DatabaseSizePoint(
                g.Key,
                g.Sum(x => x.DataBytes),
                g.Sum(x => x.LogBytes),
                g.Sum(x => x.DataBytes) + g.Sum(x => x.LogBytes)))
            .ToListAsync(ct).ConfigureAwait(false);

        var databases = await LastSnapshotDatabaseRowsAsync(inRange, ct).ConfigureAwait(false);

        return TypedResults.Ok(new DatabaseSizeTenantSeriesResponse(
            points, databases, rangeFrom, rangeTo, clamped, MaxSpan.Days));
    }

    // Разбивка по базам клиента на последний снимок периода.
    private static async Task<List<DatabaseSizeDatabaseRow>> LastSnapshotDatabaseRowsAsync(
        IQueryable<DatabaseSizeSnapshot> inRange, CancellationToken ct)
    {
        var lastAt = await inRange
            .MaxAsync(x => (DateTime?)x.SnapshotAtUtc, ct).ConfigureAwait(false);
        if (lastAt is not { } last)
        {
            return [];
        }

        return await inRange
            .Where(x => x.SnapshotAtUtc == last)
            .GroupBy(x => x.DatabaseName)
            .OrderByDescending(g => g.Sum(x => x.DataBytes) + g.Sum(x => x.LogBytes))
            .ThenBy(g => g.Key)
            .Select(g => new DatabaseSizeDatabaseRow(
                g.Key,
                g.Sum(x => x.DataBytes),
                g.Sum(x => x.LogBytes),
                g.Sum(x => x.DataBytes) + g.Sum(x => x.LogBytes)))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    // Период-сводка из готового ряда. Пик — самый ранний бакет с максимальным ConsumedMax
    // (ряд отсортирован по возрастанию, MaxBy возвращает первый из равных). Пустой ряд = не
    // ошибка: нули + null-пик (под empty-state FE).
    private static LicenseUsageSeriesResponse BuildResponse(
        List<LicenseUsageBucketPoint> buckets, DateTime from, DateTime to, bool clamped, int maxSpanDays)
    {
        if (buckets.Count == 0)
        {
            return new LicenseUsageSeriesResponse(buckets, from, to, 0, 0, null, 0, clamped, maxSpanDays);
        }

        var peak = buckets.MaxBy(b => b.ConsumedMax)!;
        var average = buckets.Average(b => b.ConsumedAvg);

        return new LicenseUsageSeriesResponse(
            buckets, from, to, peak.ConsumedMax, peak.Limit, peak.BucketStartUtc, average, clamped, maxSpanDays);
    }

    private static Dictionary<string, string[]>? RangeError(DateTime? from, DateTime? to)
    {
        if (from is { } f && to is { } t && t < f)
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(to)] = ["Конец диапазона раньше начала."],
            };
        }

        return null;
    }

    // Дефолт: to=now, from=now-DefaultWindow. Ширину >MaxSpan кламп двигает from вперёд —
    // эффективный диапазон возвращается в ответе (FromUtc/ToUtc), а факт обрезки — флагом
    // Clamped (MLC-054). Дефолтное окно 7 дней ветку клампа не задевает → Clamped=false.
    internal static (DateTime From, DateTime To, bool Clamped) ResolveRange(DateTime? from, DateTime? to, DateTime now)
    {
        var effectiveTo = to ?? now;
        var effectiveFrom = from ?? effectiveTo - DefaultWindow;

        var clamped = effectiveTo - effectiveFrom > MaxSpan;
        if (clamped)
        {
            effectiveFrom = effectiveTo - MaxSpan;
        }

        return (effectiveFrom, effectiveTo, clamped);
    }
}
