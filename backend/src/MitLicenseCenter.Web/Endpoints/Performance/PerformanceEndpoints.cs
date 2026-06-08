using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

// Раздел «Быстродействие» (MLC-064, ADR-26). Live host-снимок: pull-по-требованию,
// ничего не персистится (фронт поллит ~5с, пока вкладка открыта). Vertical slice (ADR-20)
// — эндпоинт зовёт Application-порт IHostMetricsProbe, к WMI/Process напрямую не ходит.
// Чтение = Viewer (управление записью прибудет в Фазе 4 как Admin, ADR-26).
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
}
