using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

public static class DashboardEndpoints
{
    internal const string CacheKey = "dashboard:summary";
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

    public static void MapDashboardEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/dashboard")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Dashboard");

        group.MapGet("/summary", SummaryAsync).RequireAuthorization(Roles.Viewer);
    }

    internal static async Task<Ok<DashboardSummaryResponse>> SummaryAsync(
        AppDbContext db,
        IActiveSessionSnapshotStore snapshot,
        IRasHealthReader rasHealth,
        IMemoryCache cache,
        CancellationToken ct)
    {
        // Кэшируем dashboard на 5 секунд: snapshot всё равно обновляется не чаще,
        // а UI polling — 15s. Один и тот же ответ для Viewer и Admin (нет per-user
        // тонкостей), поэтому ключ глобальный.
        var response = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await ComputeAsync(db, snapshot, rasHealth, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return TypedResults.Ok(response!);
    }

    internal static async Task<DashboardSummaryResponse> ComputeAsync(
        AppDbContext db,
        IActiveSessionSnapshotStore snapshot,
        IRasHealthReader rasHealth,
        CancellationToken ct)
    {
        // Один проход по Tenants: total/active count + map id→(name, limit) для top-5.
        var tenants = await db.Tenants
            .AsNoTracking()
            .Select(t => new TenantAggregateRow(t.Id, t.Name, t.MaxConcurrentLicenses, t.IsActive))
            .ToListAsync(ct).ConfigureAwait(false);

        var tenantsTotal = tenants.Count;
        var tenantsActive = tenants.Count(t => t.IsActive);

        var infobasesTotal = await db.Infobases.AsNoTracking().CountAsync(ct).ConfigureAwait(false);

        var payload = snapshot.Current();
        var sessionsActiveTotal = payload.Items.Count;
        var licensesConsumedTotal = payload.Items.Count(s => s.ConsumesLicense);

        // Сумма лимитов — только по активным тенантам; неактивный tenant'у
        // не должен «вешать» свою квоту на свободную ёмкость.
        var totalLimits = tenants.Where(t => t.IsActive).Sum(t => t.MaxConcurrentLicenses);
        var licensesAvailableTotal = Math.Max(0, totalLimits - licensesConsumedTotal);

        var consumedByTenant = LicenseConsumption.CountByTenant(payload.Items);

        // Top-5 ranking: percent (с защитой от div-by-zero) → consumed (tiebreaker).
        // Tenant с limit=0 даёт percent=0 — не попадает в топ, если есть кто-то с >0%.
        var topTenants = tenants
            .Select(t =>
            {
                var consumed = consumedByTenant.GetValueOrDefault(t.Id, 0);
                var percent = t.MaxConcurrentLicenses > 0
                    ? (int)Math.Round(consumed * 100.0 / t.MaxConcurrentLicenses)
                    : 0;
                return new TenantConsumptionRow(t.Id, t.Name, consumed, t.MaxConcurrentLicenses, percent);
            })
            .Where(row => row.Consumed > 0 || row.Percent > 0)
            .OrderByDescending(row => row.Percent)
            .ThenByDescending(row => row.Consumed)
            .ThenBy(row => row.TenantName)
            .Take(5)
            .ToList();

        var health = rasHealth.GetSnapshot();
        var ras = new DashboardRasHealth(
            Healthy: health.Healthy,
            LastCheckedAtUtc: health.LastCheckedAtUtc,
            LastErrorMessage: health.LastErrorMessage,
            ConsecutiveFailures: health.ConsecutiveFailures);

        return new DashboardSummaryResponse(
            TenantsTotal: tenantsTotal,
            TenantsActive: tenantsActive,
            InfobasesTotal: infobasesTotal,
            SessionsActiveTotal: sessionsActiveTotal,
            LicensesConsumedTotal: licensesConsumedTotal,
            LicensesAvailableTotal: licensesAvailableTotal,
            TopTenantsByConsumption: topTenants,
            Ras: ras);
    }

    private readonly record struct TenantAggregateRow(Guid Id, string Name, int MaxConcurrentLicenses, bool IsActive);
}
