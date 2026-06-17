using System.Security.Claims;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

public static class DashboardEndpoints
{
    internal const string CacheKey = "dashboard:summary";
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

    // MLC-186a — алерты «Требует внимания». Два ключа (ответ зависит от роли: дрейф панель↔кластер
    // — Admin-only, см. AlertsAsync). TTL 30с: источники тяжелее summary (снапшот RAS + SQL-диск),
    // и Viewer-путь вовсе не опрашивает кластер — Viewer не триггерит rac.
    internal const string AlertsCacheKeyAdmin = "dashboard:alerts:admin";
    internal const string AlertsCacheKeyViewer = "dashboard:alerts:viewer";
    internal static readonly TimeSpan AlertsCacheTtl = TimeSpan.FromSeconds(30);

    // Пороги severity квоты лицензий — зеркало frontend/src/lib/quota.ts (держать в синхроне):
    // danger pct ≥ 90, warning 75 ≤ pct < 90. Бакеты непересекающиеся.
    private const int QuotaWarningThreshold = 75;
    private const int QuotaDangerThreshold = 90;

    public static void MapDashboardEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/dashboard")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Dashboard");

        group.MapGet("/summary", SummaryAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/alerts", AlertsAsync).RequireAuthorization(Roles.Viewer);
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
        // ADR-48 (MLC-166): потребление = факт rac (LicenseStatus==Consuming), не эвристика.
        var licensesConsumedTotal = payload.Items.Count(s => s.LicenseStatus == LicenseStatus.Consuming);

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
            LicenseFactAvailable: payload.LicenseFactAvailable,
            TopTenantsByConsumption: topTenants,
            Ras: ras);
    }

    // MLC-186a — серверный агрегат сигналов «Требует внимания» (отдельно от лёгкого /summary):
    // нарушители квоты + дрейф панель↔кластер + мало места на диске бэкапов. Дрейф — Admin-only
    // (discovery Admin-only, как /infobases/unassigned): для Viewer ClusterDrift=null И кластер
    // НЕ опрашивается (Viewer не триггерит rac). Кэш — per-role (ответ зависит от роли).
    internal static async Task<Ok<DashboardAlertsResponse>> AlertsAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        IActiveSessionSnapshotStore snapshot,
        IClusterClient cluster,
        UnassignedInfobasesCache clusterCache,
        ISettingsSnapshot settings,
        ISqlBackupService backupService,
        ILoggerFactory loggerFactory,
        TimeProvider clock,
        IMemoryCache cache,
        CancellationToken ct)
    {
        var isAdmin = user.IsInRole(Roles.Admin);
        var key = isAdmin ? AlertsCacheKeyAdmin : AlertsCacheKeyViewer;

        var response = await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = AlertsCacheTtl;
            return await ComputeAlertsAsync(
                isAdmin, db, snapshot, cluster, clusterCache, settings, backupService,
                loggerFactory, clock, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return TypedResults.Ok(response!);
    }

    internal static async Task<DashboardAlertsResponse> ComputeAlertsAsync(
        bool isAdmin,
        AppDbContext db,
        IActiveSessionSnapshotStore snapshot,
        IClusterClient cluster,
        UnassignedInfobasesCache clusterCache,
        ISettingsSnapshot settings,
        ISqlBackupService backupService,
        ILoggerFactory loggerFactory,
        TimeProvider clock,
        CancellationToken ct)
    {
        // ── Нарушители квоты — по ВСЕМ активным тенантам с положительным лимитом (не только
        // топ-5 summary). Потребление = факт rac (LicenseStatus==Consuming), как везде (ADR-48).
        var activeLimits = await db.Tenants.AsNoTracking()
            .Where(t => t.IsActive && t.MaxConcurrentLicenses > 0)
            .Select(t => new { t.Id, t.MaxConcurrentLicenses })
            .ToListAsync(ct).ConfigureAwait(false);

        var consumedByTenant = LicenseConsumption.CountByTenant(snapshot.Current().Items);

        var quotaWarning = 0;
        var quotaDanger = 0;
        foreach (var t in activeLimits)
        {
            var consumed = consumedByTenant.GetValueOrDefault(t.Id, 0);
            var percent = (int)Math.Round(consumed * 100.0 / t.MaxConcurrentLicenses);
            if (percent >= QuotaDangerThreshold)
            {
                quotaDanger++;
            }
            else if (percent >= QuotaWarningThreshold)
            {
                quotaWarning++;
            }
        }

        // ── Дрейф панель↔кластер — только Admin (discovery Admin-only). Viewer-путь не трогает
        // кластер (rac не спавнится). Общий снапшот через тот же TTL-кэш, что list-эндпоинт.
        DashboardClusterDriftAlert? clusterDrift = null;
        if (isAdmin)
        {
            var clusterSnapshot = await UnassignedInfobasesEndpoints
                .GetClusterSnapshotAsync(cluster, clusterCache, loggerFactory, clock, refresh: null, ct)
                .ConfigureAwait(false);

            if (!clusterSnapshot.Available)
            {
                clusterDrift = new DashboardClusterDriftAlert(Available: false, null, null);
            }
            else
            {
                var (unassigned, notInCluster) = await UnassignedInfobasesEndpoints
                    .CountDriftAsync(db, clusterSnapshot, ct).ConfigureAwait(false);
                clusterDrift = new DashboardClusterDriftAlert(Available: true, unassigned, notInCluster);
            }
        }

        // ── Мало места на диске бэкапов (MLC-183 disk-guard): free < склампленный safety margin.
        // Папка/сервер не заданы → Configured=false (без обращения к SQL). FreeBytes=null → «не знаем».
        var backupDisk = await ComputeBackupDiskAsync(settings, backupService, ct).ConfigureAwait(false);

        return new DashboardAlertsResponse(quotaWarning, quotaDanger, clusterDrift, backupDisk);
    }

    private static async Task<DashboardBackupDiskAlert> ComputeBackupDiskAsync(
        ISettingsSnapshot settings, ISqlBackupService backupService, CancellationToken ct)
    {
        var folderRoot = settings.GetString(SettingKey.BackupFolderPath);
        var sqlServer = settings.GetString(SettingKey.SqlServer);
        if (string.IsNullOrWhiteSpace(folderRoot) || string.IsNullOrWhiteSpace(sqlServer))
        {
            return new DashboardBackupDiskAlert(Configured: false, FreeBytes: null, SafetyMarginBytes: 0, Low: false);
        }

        var marginMb = SettingDefinitions.ClampToRange(
            SettingKey.BackupDiskSafetyMarginMb,
            settings.GetInt(SettingKey.BackupDiskSafetyMarginMb) ?? BackupsEndpoints.DefaultDiskSafetyMarginMb);
        var marginBytes = (long)marginMb * 1024 * 1024;

        var free = await backupService.GetBackupDiskFreeBytesAsync(sqlServer, folderRoot, ct).ConfigureAwait(false);
        var low = free is { } f && f < marginBytes;
        return new DashboardBackupDiskAlert(Configured: true, free, marginBytes, low);
    }

    private readonly record struct TenantAggregateRow(Guid Id, string Name, int MaxConcurrentLicenses, bool IsActive);
}
