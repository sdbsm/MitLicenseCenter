using Asp.Versioning;
using Asp.Versioning.Builder;
using Hangfire;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

// Publications в Stage 2 управляются в составе aggregate'а Infobase (см.
// InfobasesEndpoints). Этот модуль оставляем для прямого редактирования полей
// IIS-публикации — например, чтобы сменить PlatformVersion без открытия формы
// инфобазы; full create/delete намеренно отсутствуют (это домен Infobase).
//
// PR 3.5 добавляет drift-операции: проверка дрейфа default.vrd, чтение
// последнего drift-статуса, согласование (reconcile) — surgical XML-patch.
public static class PublicationsEndpoints
{
    public static void MapPublicationsEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/publications")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Publications");

        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization(Roles.Viewer);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(Roles.Admin);

        // PR 3.5: drift-операции.
        group.MapPost("/{id:guid}/check-drift", CheckDriftAsync).RequireAuthorization(Roles.Admin);
        group.MapGet("/{id:guid}/drift-status", DriftStatusAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("/{id:guid}/reconcile", ReconcileAsync).RequireAuthorization(Roles.Admin);
    }

    private static async Task<Results<Ok<PublicationResponse>, NotFound>> GetAsync(
        Guid id,
        AppDbContext db,
        CancellationToken ct)
    {
        var publication = await db.Publications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        return publication is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(publication.ToResponse());
    }

    private static async Task<Results<Ok<PublicationResponse>, NotFound, ValidationProblem>> UpdateAsync(
        Guid id,
        [FromBody] UpdatePublicationRequest request,
        AppDbContext db,
        IAuditLogger audit,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        var publication = await db.Publications.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (publication is null)
        {
            return TypedResults.NotFound();
        }

        var siteName = (request.SiteName ?? string.Empty).Trim();
        var virtualPath = (request.VirtualPath ?? string.Empty).Trim();
        var platformVersion = (request.PlatformVersion ?? string.Empty).Trim();

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(siteName))
        {
            errors[nameof(UpdatePublicationRequest.SiteName)] = ["Укажите имя сайта IIS."];
        }
        if (string.IsNullOrEmpty(virtualPath))
        {
            errors[nameof(UpdatePublicationRequest.VirtualPath)] = ["Укажите виртуальный путь."];
        }
        else if (!virtualPath.StartsWith('/'))
        {
            errors[nameof(UpdatePublicationRequest.VirtualPath)] = ["Виртуальный путь должен начинаться с «/»."];
        }
        else if (virtualPath.Any(char.IsWhiteSpace))
        {
            errors[nameof(UpdatePublicationRequest.VirtualPath)] = ["Виртуальный путь не должен содержать пробелов."];
        }
        if (string.IsNullOrEmpty(platformVersion))
        {
            errors[nameof(UpdatePublicationRequest.PlatformVersion)] = ["Укажите версию платформы 1С."];
        }
        else if (!InfobasesEndpoints.IsValidPlatformVersion(platformVersion))
        {
            errors[nameof(UpdatePublicationRequest.PlatformVersion)] = ["Версия должна состоять из четырёх числовых сегментов, например «8.3.23.1865» или «8.5.1.1302»."];
        }
        // Physical-path override (PR 4.1): принимаем только абсолютные пути.
        if (!string.IsNullOrWhiteSpace(request.PhysicalPathOverride)
            && !Path.IsPathFullyQualified(request.PhysicalPathOverride.Trim()))
        {
            errors[nameof(UpdatePublicationRequest.PhysicalPathOverride)] =
                ["Укажите абсолютный путь к папке (например, C:\\pub\\app или \\\\server\\share\\app)."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var infobase = await db.Infobases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == publication.InfobaseId, ct).ConfigureAwait(false);

        publication.SiteName = siteName;
        publication.VirtualPath = virtualPath;
        publication.PlatformVersion = platformVersion;
        publication.EnableOData = request.EnableOData;
        publication.EnableHttpServices = request.EnableHttpServices;
        publication.VrdCustomXml = string.IsNullOrWhiteSpace(request.VrdCustomXml) ? null : request.VrdCustomXml;
        publication.PhysicalPathOverride = string.IsNullOrWhiteSpace(request.PhysicalPathOverride)
            ? null
            : request.PhysicalPathOverride.Trim().TrimEnd('\\', '/');
        publication.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var initiator = httpContext.User.Identity?.Name ?? "unknown";
        await audit.LogAsync(
            AuditActionType.PublicationUpdated,
            initiator: initiator,
            description: $"Публикация «{publication.SiteName}{publication.VirtualPath}» обновлена администратором {initiator}.",
            tenantId: infobase?.TenantId,
            ct: ct).ConfigureAwait(false);

        return TypedResults.Ok(publication.ToResponse());
    }

    internal static async Task<Results<Accepted<CheckDriftAcceptedResponse>, NotFound>> CheckDriftAsync(
        Guid id,
        AppDbContext db,
        IBackgroundJobClient jobs,
        CancellationToken ct)
    {
        var exists = await db.Publications.AsNoTracking().AnyAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (!exists)
        {
            return TypedResults.NotFound();
        }

        // Off-thread: запускаем Hangfire-job, не дожидаемся завершения. UI
        // polls /drift-status каждые 2s (план PR 3.6). 202 + correlationId,
        // чтобы оператор мог найти job в Hangfire dashboard при разборе.
        var jobId = jobs.Enqueue<IDriftCheckJob>(j => j.CheckOneAsync(id, CancellationToken.None));
        return TypedResults.Accepted(
            uri: $"/api/v1/publications/{id}/drift-status",
            value: new CheckDriftAcceptedResponse(jobId, id));
    }

    internal static async Task<Results<Ok<DriftStatusResponse>, NotFound>> DriftStatusAsync(
        Guid id,
        AppDbContext db,
        CancellationToken ct)
    {
        var publication = await db.Publications
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new DriftStatusResponse(x.LastDriftStatus, x.LastDriftCheckAt, x.LastDriftDetails))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return publication is null ? TypedResults.NotFound() : TypedResults.Ok(publication);
    }

    internal static async Task<Results<Ok<DriftStatusResponse>, NotFound, Conflict<ProblemDetails>>> ReconcileAsync(
        Guid id,
        AppDbContext db,
        IIisPublishingService iis,
        IDriftCheckJob driftJob,
        IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var publication = await db.Publications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (publication is null)
        {
            return TypedResults.NotFound();
        }

        var previousStatus = publication.LastDriftStatus;

        // ApplyDesiredStateAsync — surgical XML-patch. При IIS-исключении
        // короткое замыкание на 409: НЕ обновляем drift-статус и НЕ пишем
        // аудит (план PR 3.5: «не записывать audit при отказе»).
        try
        {
            await iis.ApplyDesiredStateAsync(publication, ct).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            return TypedResults.Conflict(Problems.IisAccessDenied(ex.Message));
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            return TypedResults.Conflict(Problems.IisReconcileFailed(ex.Message));
        }
        catch (IOException ex)
        {
            return TypedResults.Conflict(Problems.IisReconcileFailed(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            // Невалидный VrdCustomXml, отсутствие default.vrd, и другие
            // прикладные сбои согласования — все пакуем под единый код 409.
            return TypedResults.Conflict(Problems.IisReconcileFailed(ex.Message));
        }

        // Повторно прогоняем drift-check, чтобы вернуть оператору актуальный
        // статус после patch'а — и чтобы LastDriftStatus/At/Details обновились
        // synchronously. DriftCheckJob не пишет 210 при transition в InSync, так
        // что мы не получим лишнюю audit-строку.
        await driftJob.CheckOneAsync(id, ct).ConfigureAwait(false);

        var updated = await db.Publications
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                Status = x.LastDriftStatus,
                CheckedAt = x.LastDriftCheckAt,
                Details = x.LastDriftDetails,
                x.SiteName,
                x.VirtualPath,
                x.InfobaseId,
            })
            .FirstAsync(ct)
            .ConfigureAwait(false);

        var tenantId = await db.Infobases
            .AsNoTracking()
            .Where(i => i.Id == updated.InfobaseId)
            .Select(i => (Guid?)i.TenantId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var initiator = httpContext.User.Identity?.Name ?? "unknown";
        await audit.LogAsync(
            AuditActionType.PublicationReconciled,
            initiator: initiator,
            description: $"Публикация {updated.SiteName}{updated.VirtualPath} согласована оператором {initiator}: статус {previousStatus} → {updated.Status}.",
            tenantId: tenantId,
            ct: ct).ConfigureAwait(false);

        return TypedResults.Ok(new DriftStatusResponse(updated.Status, updated.CheckedAt, updated.Details));
    }
}
