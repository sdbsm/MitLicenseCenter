using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

// Publications в Stage 2 управляются в составе aggregate'а Infobase (см.
// InfobasesEndpoints). Этот модуль оставляем для прямого редактирования полей
// IIS-публикации — например, чтобы сменить PlatformVersion без открытия формы
// инфобазы; full create/delete намеренно отсутствуют (это домен Infobase).
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
            errors[nameof(UpdatePublicationRequest.PlatformVersion)] = ["Версия должна быть в формате «8.3.23.1865»."];
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
}
