using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

// Раздел «Быстродействие» (MLC-064/066/068, ADR-26). Live-снимки: pull-по-требованию,
// ничего не персистится (фронт поллит ~5с, пока вкладка открыта). Vertical slice (ADR-20)
// — эндпоинты зовут Application-порты (IHostMetricsProbe / IClusterClient / ISqlPerformanceProbe),
// к WMI/Process/rac.exe/DMV напрямую не ходят (но свой AppDbContext читать можно — атрибуция SQL
// по клиенту). Чтение = Viewer (управление записью прибудет в Фазе 4 как Admin).
public static class PerformanceEndpoints
{
    public static void MapPerformanceEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/performance")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Performance");

        group.MapGet("/host", GetHostAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/onec-sessions", GetOneCSessionsAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/sql", GetSqlAsync).RequireAuthorization(Roles.Viewer);
    }

    // Live-снимок метрик хоста «сейчас». Measuring=true на первом poll'е (CPU% процессов и
    // латентность диска требуют дельты двух замеров). Адаптер защитный — недоступная метрика
    // деградирует в 0, снимок отдаётся всегда.
    internal static async Task<Ok<HostMetricsSnapshot>> GetHostAsync(
        [FromServices] IHostMetricsProbe probe,
        CancellationToken ct)
    {
        var snapshot = await probe.CaptureAsync(ct).ConfigureAwait(false);
        return TypedResults.Ok(snapshot);
    }

    // Live-снимок нагрузки 1С «кто грузит» (MLC-066): сеансы с perf-полями (`rac session list`)
    // + рабочие процессы (`rac process list`) — 2 спавна rac.exe на poll (spawn-бюджет ADR-3.3).
    // Пустые списки = rac не настроен/недоступен (best-effort). Ничего не персистится (ADR-26).
    internal static async Task<Ok<OneCLoadSnapshot>> GetOneCSessionsAsync(
        [FromServices] IClusterClient cluster,
        CancellationToken ct)
    {
        var sessions = await cluster.ListSessionLoadsAsync(ct).ConfigureAwait(false);
        var processes = await cluster.ListProcessesAsync(ct).ConfigureAwait(false);
        return TypedResults.Ok(new OneCLoadSnapshot(DateTime.UtcNow, sessions, processes));
    }

    // Live-снимок нагрузки на MSSQL «1С грузит SQL?» (MLC-068, Фаза 3): активные запросы +
    // цепочки блокировок + IO-stall + дельта wait-stats через порт ISqlPerformanceProbe (только
    // он ходит в DMV — ADR-20). Эндпоинт добавляет атрибуцию по клиенту: сшивает имена баз из
    // DMV с инфобазами панели (database→Infobase→tenant) по своему AppDbContext — это разрешённый
    // прямой доступ vertical slice к собственной БД, не к DMV. Нет права VIEW SERVER STATE →
    // снимок со Status=PermissionDenied (фронт рисует degraded-баннер), не пустой «всё спокойно».
    internal static async Task<Ok<SqlPerformanceView>> GetSqlAsync(
        [FromServices] ISqlPerformanceProbe probe,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var snapshot = await probe.CaptureAsync(ct).ConfigureAwait(false);

        var refs = await db.Infobases
            .Join(
                db.Tenants,
                ib => ib.TenantId,
                t => t.Id,
                (ib, t) => new InfobaseDatabaseRef(ib.DatabaseName, ib.TenantId, t.Name, ib.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var attribution = SqlAttributionResolver.Resolve(snapshot.DatabaseNames(), refs);
        return TypedResults.Ok(new SqlPerformanceView(snapshot, attribution));
    }
}
