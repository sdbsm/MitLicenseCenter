using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

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
