using System.Text.RegularExpressions;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

public static partial class InfobasesEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    // Версия платформы 1С — 4 числовых сегмента «Major.Minor.Build.Revision».
    // Длины сегментов не фиксируем: реальные сборки бывают и «8.3.23.1865», и «8.5.1.1302».
    [GeneratedRegex(@"^\d+\.\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex PlatformVersionRegex();

    public static void MapInfobasesEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/infobases")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Infobases");

        group.MapGet("/", ListAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("/", CreateAsync).RequireAuthorization(Roles.Admin);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(Roles.Admin);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(Roles.Admin);
    }

    private static async Task<Ok<InfobaseListResponse>> ListAsync(
        AppDbContext db,
        [FromQuery] Guid? tenantId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var p = page is > 0 ? page.Value : 1;
        var ps = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        var baseQuery = db.Infobases.AsNoTracking();
        if (tenantId is { } tid)
        {
            baseQuery = baseQuery.Where(x => x.TenantId == tid);
        }

        var orderedQuery = baseQuery.OrderBy(x => x.Name).ThenBy(x => x.Id);
        var total = await orderedQuery.CountAsync(ct).ConfigureAwait(false);

        // Join'им Tenant и Publication одним запросом — UI выводит «Клиент» и
        // компактный «VirtualPath, PlatformVersion» прямо в строке таблицы.
        var items = await orderedQuery
            .Skip((p - 1) * ps)
            .Take(ps)
            .Join(
                db.Tenants.AsNoTracking(),
                ib => ib.TenantId,
                t => t.Id,
                (ib, t) => new { Infobase = ib, TenantName = t.Name })
            .Join(
                db.Publications.AsNoTracking(),
                x => x.Infobase.Id,
                pub => pub.InfobaseId,
                (x, pub) => new InfobaseListItemResponse(
                    x.Infobase.Id,
                    x.Infobase.TenantId,
                    x.TenantName,
                    x.Infobase.Name,
                    x.Infobase.ClusterInfobaseId,
                    x.Infobase.DatabaseServer,
                    x.Infobase.DatabaseName,
                    x.Infobase.Status,
                    x.Infobase.CreatedAt,
                    x.Infobase.UpdatedAt,
                    new PublicationResponse(
                        pub.Id,
                        pub.InfobaseId,
                        pub.SiteName,
                        pub.VirtualPath,
                        pub.PlatformVersion,
                        pub.EnableOData,
                        pub.EnableHttpServices,
                        pub.VrdCustomXml,
                        pub.CreatedAt,
                        pub.UpdatedAt,
                        pub.LastDriftStatus,
                        pub.LastDriftCheckAt,
                        pub.LastDriftDetails,
                        pub.PhysicalPathOverride)))
            .ToListAsync(ct).ConfigureAwait(false);

        return TypedResults.Ok(new InfobaseListResponse(items, total, p, ps));
    }

    private static async Task<Results<Ok<InfobaseDetailResponse>, NotFound>> GetAsync(
        Guid id,
        AppDbContext db,
        CancellationToken ct)
    {
        var infobase = await db.Infobases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (infobase is null)
        {
            return TypedResults.NotFound();
        }

        var publication = await db.Publications.AsNoTracking().FirstOrDefaultAsync(p => p.InfobaseId == id, ct).ConfigureAwait(false);
        if (publication is null)
        {
            // 1-to-1 required в схеме — рассинхрон может произойти только при ручной
            // правке БД. Возвращаем 404, чтобы не отдавать половину aggregate'а.
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(infobase.ToDetailResponse(publication));
    }

    internal static async Task<Results<Created<InfobaseDetailResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> CreateAsync(
        [FromBody] CreateInfobaseRequest request,
        AppDbContext db,
        IAuditLogger audit,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        var normalizedName = (request.Name ?? string.Empty).Trim();
        var normalizedDbServer = (request.DatabaseServer ?? string.Empty).Trim();
        var normalizedDbName = (request.DatabaseName ?? string.Empty).Trim();

        var errors = ValidateInfobase(normalizedName, normalizedDbServer, normalizedDbName, request.Status);
        AppendPublicationErrors(errors, request.Publication);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!await db.Tenants.AnyAsync(t => t.Id == request.TenantId, ct).ConfigureAwait(false))
        {
            return TypedResults.NotFound();
        }

        if (await db.Infobases.AnyAsync(x => x.TenantId == request.TenantId && x.Name == normalizedName, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.InfobaseNameDuplicateInTenant(normalizedName));
        }

        // Одна база кластера принадлежит только одному клиенту — проверка глобальная,
        // без фильтра по TenantId.
        if (await db.Infobases.AnyAsync(x => x.ClusterInfobaseId == request.ClusterInfobaseId, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.InfobaseAlreadyAssigned());
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var infobase = new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Name = normalizedName,
            ClusterInfobaseId = request.ClusterInfobaseId,
            DatabaseServer = normalizedDbServer,
            DatabaseName = normalizedDbName,
            Status = request.Status,
            CreatedAt = now,
        };

        var publication = new Publication
        {
            Id = Guid.NewGuid(),
            InfobaseId = infobase.Id,
            SiteName = request.Publication.SiteName.Trim(),
            VirtualPath = request.Publication.VirtualPath.Trim(),
            PlatformVersion = request.Publication.PlatformVersion.Trim(),
            EnableOData = request.Publication.EnableOData,
            EnableHttpServices = request.Publication.EnableHttpServices,
            VrdCustomXml = string.IsNullOrWhiteSpace(request.Publication.VrdCustomXml) ? null : request.Publication.VrdCustomXml,
            PhysicalPathOverride = string.IsNullOrWhiteSpace(request.Publication.PhysicalPathOverride)
                ? null
                : request.Publication.PhysicalPathOverride.Trim().TrimEnd('\\', '/'),
            CreatedAt = now,
        };

        // Инфобаза + публикация — один aggregate, попадают в БД одним SaveChanges.
        db.Infobases.Add(infobase);
        db.Publications.Add(publication);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var initiator = httpContext.User.Identity?.Name ?? "unknown";
        await audit.LogAsync(
            AuditActionType.InfobaseCreated,
            initiator: initiator,
            description: $"Инфобаза «{infobase.Name}» создана администратором {initiator}.",
            tenantId: infobase.TenantId,
            ct: ct).ConfigureAwait(false);
        await audit.LogAsync(
            AuditActionType.PublicationCreated,
            initiator: initiator,
            description: $"Публикация «{publication.SiteName}{publication.VirtualPath}» создана для инфобазы «{infobase.Name}» администратором {initiator}.",
            tenantId: infobase.TenantId,
            ct: ct).ConfigureAwait(false);

        return TypedResults.Created($"/api/v1/infobases/{infobase.Id}", infobase.ToDetailResponse(publication));
    }

    internal static async Task<Results<Ok<InfobaseDetailResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateInfobaseRequest request,
        AppDbContext db,
        IAuditLogger audit,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        var infobase = await db.Infobases.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (infobase is null)
        {
            return TypedResults.NotFound();
        }

        var publication = await db.Publications.FirstOrDefaultAsync(p => p.InfobaseId == id, ct).ConfigureAwait(false);
        if (publication is null)
        {
            return TypedResults.NotFound();
        }

        var normalizedName = (request.Name ?? string.Empty).Trim();
        var normalizedDbServer = (request.DatabaseServer ?? string.Empty).Trim();
        var normalizedDbName = (request.DatabaseName ?? string.Empty).Trim();

        var errors = ValidateInfobase(normalizedName, normalizedDbServer, normalizedDbName, request.Status);
        AppendPublicationErrors(errors, request.Publication);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!string.Equals(infobase.Name, normalizedName, StringComparison.Ordinal)
            && await db.Infobases.AnyAsync(x => x.TenantId == infobase.TenantId && x.Name == normalizedName && x.Id != id, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.InfobaseNameDuplicateInTenant(normalizedName));
        }

        // Смена базы кластера на уже привязанную к другому клиенту — конфликт.
        if (infobase.ClusterInfobaseId != request.ClusterInfobaseId
            && await db.Infobases.AnyAsync(x => x.ClusterInfobaseId == request.ClusterInfobaseId && x.Id != id, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.InfobaseAlreadyAssigned());
        }

        var now = clock.GetUtcNow().UtcDateTime;
        infobase.Name = normalizedName;
        infobase.ClusterInfobaseId = request.ClusterInfobaseId;
        infobase.DatabaseServer = normalizedDbServer;
        infobase.DatabaseName = normalizedDbName;
        infobase.Status = request.Status;
        infobase.UpdatedAt = now;

        publication.SiteName = request.Publication.SiteName.Trim();
        publication.VirtualPath = request.Publication.VirtualPath.Trim();
        publication.PlatformVersion = request.Publication.PlatformVersion.Trim();
        publication.EnableOData = request.Publication.EnableOData;
        publication.EnableHttpServices = request.Publication.EnableHttpServices;
        publication.VrdCustomXml = string.IsNullOrWhiteSpace(request.Publication.VrdCustomXml) ? null : request.Publication.VrdCustomXml;
        publication.PhysicalPathOverride = string.IsNullOrWhiteSpace(request.Publication.PhysicalPathOverride)
            ? null
            : request.Publication.PhysicalPathOverride.Trim().TrimEnd('\\', '/');
        publication.UpdatedAt = now;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var initiator = httpContext.User.Identity?.Name ?? "unknown";
        await audit.LogAsync(
            AuditActionType.InfobaseUpdated,
            initiator: initiator,
            description: $"Инфобаза «{infobase.Name}» обновлена администратором {initiator}.",
            tenantId: infobase.TenantId,
            ct: ct).ConfigureAwait(false);
        await audit.LogAsync(
            AuditActionType.PublicationUpdated,
            initiator: initiator,
            description: $"Публикация «{publication.SiteName}{publication.VirtualPath}» обновлена для инфобазы «{infobase.Name}» администратором {initiator}.",
            tenantId: infobase.TenantId,
            ct: ct).ConfigureAwait(false);

        return TypedResults.Ok(infobase.ToDetailResponse(publication));
    }

    internal static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id,
        AppDbContext db,
        IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var infobase = await db.Infobases.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (infobase is null)
        {
            return TypedResults.NotFound();
        }

        var publication = await db.Publications.FirstOrDefaultAsync(p => p.InfobaseId == id, ct).ConfigureAwait(false);

        var initiator = httpContext.User.Identity?.Name ?? "unknown";
        var infobaseName = infobase.Name;
        var tenantId = infobase.TenantId;
        var publicationLabel = publication is null
            ? null
            : $"{publication.SiteName}{publication.VirtualPath}";

        // Аудит пишем ДО удаления — TenantId ещё валиден, FK не нарушается.
        if (publication is not null)
        {
            await audit.LogAsync(
                AuditActionType.PublicationDeleted,
                initiator: initiator,
                description: $"Публикация «{publicationLabel}» удалена вместе с инфобазой «{infobaseName}» администратором {initiator}.",
                tenantId: tenantId,
                ct: ct).ConfigureAwait(false);
        }
        await audit.LogAsync(
            AuditActionType.InfobaseDeleted,
            initiator: initiator,
            description: $"Инфобаза «{infobaseName}» удалена администратором {initiator}.",
            tenantId: tenantId,
            ct: ct).ConfigureAwait(false);

        // FK Publication→Infobase = Cascade на стороне БД, но InMemory-провайдер в
        // тестах его не уважает. Сносим публикацию вручную — поведение одинаковое.
        if (publication is not null)
        {
            db.Publications.Remove(publication);
        }
        db.Infobases.Remove(infobase);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static Dictionary<string, string[]> ValidateInfobase(
        string normalizedName,
        string normalizedDbServer,
        string normalizedDbName,
        InfobaseStatus status)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            errors[nameof(CreateInfobaseRequest.Name)] = ["Название инфобазы не может быть пустым."];
        }
        if (string.IsNullOrWhiteSpace(normalizedDbServer))
        {
            errors[nameof(CreateInfobaseRequest.DatabaseServer)] = ["Укажите сервер БД."];
        }
        if (string.IsNullOrWhiteSpace(normalizedDbName))
        {
            errors[nameof(CreateInfobaseRequest.DatabaseName)] = ["Укажите имя БД."];
        }
        if (!Enum.IsDefined(status))
        {
            errors[nameof(CreateInfobaseRequest.Status)] = ["Недопустимый статус инфобазы."];
        }
        return errors;
    }

    private static void AppendPublicationErrors(
        Dictionary<string, string[]> errors,
        CreatePublicationRequest publication)
    {
        AppendPublicationFieldErrors(errors, publication.SiteName, publication.VirtualPath,
            publication.PlatformVersion, publication.PhysicalPathOverride);
    }

    private static void AppendPublicationErrors(
        Dictionary<string, string[]> errors,
        UpdatePublicationRequest publication)
    {
        AppendPublicationFieldErrors(errors, publication.SiteName, publication.VirtualPath,
            publication.PlatformVersion, publication.PhysicalPathOverride);
    }

    private static void AppendPublicationFieldErrors(
        Dictionary<string, string[]> errors,
        string? siteName,
        string? virtualPath,
        string? platformVersion,
        string? physicalPathOverride)
    {
        if (string.IsNullOrWhiteSpace(siteName))
        {
            errors[$"{nameof(CreateInfobaseRequest.Publication)}.{nameof(CreatePublicationRequest.SiteName)}"] = ["Укажите имя сайта IIS."];
        }

        var vp = (virtualPath ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(vp))
        {
            errors[$"{nameof(CreateInfobaseRequest.Publication)}.{nameof(CreatePublicationRequest.VirtualPath)}"] = ["Укажите виртуальный путь."];
        }
        else if (!vp.StartsWith('/'))
        {
            errors[$"{nameof(CreateInfobaseRequest.Publication)}.{nameof(CreatePublicationRequest.VirtualPath)}"] = ["Виртуальный путь должен начинаться с «/»."];
        }
        else if (vp.Any(char.IsWhiteSpace))
        {
            errors[$"{nameof(CreateInfobaseRequest.Publication)}.{nameof(CreatePublicationRequest.VirtualPath)}"] = ["Виртуальный путь не должен содержать пробелов."];
        }

        var version = (platformVersion ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(version))
        {
            errors[$"{nameof(CreateInfobaseRequest.Publication)}.{nameof(CreatePublicationRequest.PlatformVersion)}"] = ["Укажите версию платформы 1С."];
        }
        else if (!PlatformVersionRegex().IsMatch(version))
        {
            errors[$"{nameof(CreateInfobaseRequest.Publication)}.{nameof(CreatePublicationRequest.PlatformVersion)}"] = ["Версия должна состоять из четырёх числовых сегментов, например «8.3.23.1865» или «8.5.1.1302»."];
        }

        // Physical-path override (PR 4.1): если задан — принимаем только абсолютные пути
        // (local C:\... или UNC \\server\share\...). Relative-пути отклоняем.
        if (!string.IsNullOrWhiteSpace(physicalPathOverride)
            && !Path.IsPathFullyQualified(physicalPathOverride.Trim()))
        {
            errors[$"{nameof(CreateInfobaseRequest.Publication)}.{nameof(CreatePublicationRequest.PhysicalPathOverride)}"] =
                ["Укажите абсолютный путь к папке (например, C:\\pub\\app или \\\\server\\share\\app)."];
        }
    }

    internal static bool IsValidPlatformVersion(string value) => PlatformVersionRegex().IsMatch(value ?? string.Empty);
}
