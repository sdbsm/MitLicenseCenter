using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Publishing;
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

    public static void MapInfobasesEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/infobases")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Infobases");

        group.MapGet("/", ListAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/cluster-id-availability", ClusterIdAvailabilityAsync).RequireAuthorization(Roles.Admin);
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("/", CreateAsync).RequireAuthorization(Roles.Admin);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/reassign", ReassignAsync).RequireAuthorization(Roles.Admin);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(Roles.Admin);
    }

    internal static async Task<Results<Ok<InfobaseListResponse>, ValidationProblem>> ListAsync(
        AppDbContext db,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? publishStatus,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        // MLC-090: фильтр по статусу публикации (server-side, до пагинации — счёт честный).
        // Гоча CLAUDE.md: DataAnnotations в minimal API не валидируются в runtime, поэтому
        // значение enum'а проверяем руками (как actionType на /audit) и на мусор отвечаем 400.
        PublicationPublishStatus? parsedPublishStatus = null;
        if (!string.IsNullOrWhiteSpace(publishStatus))
        {
            if (Enum.TryParse<PublicationPublishStatus>(publishStatus, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed))
            {
                parsedPublishStatus = parsed;
            }
            else
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(publishStatus)] = ["Неизвестный статус публикации."],
                });
            }
        }

        var p = page is > 0 ? page.Value : 1;
        var ps = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        var baseQuery = db.Infobases.AsNoTracking();
        if (tenantId is { } tid)
        {
            baseQuery = baseQuery.Where(x => x.TenantId == tid);
        }
        if (parsedPublishStatus is { } status)
        {
            // Публикация 1:1 с инфобазой — коррелированный EXISTS по статусу её проверки.
            baseQuery = baseQuery.Where(x =>
                db.Publications.Any(pub => pub.InfobaseId == x.Id && pub.LastCheckStatus == status));
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
                        pub.Source,
                        pub.CreatedAt,
                        pub.UpdatedAt,
                        pub.LastCheckStatus,
                        pub.LastCheckAt,
                        pub.LastCheckDetails,
                        pub.PhysicalPathOverride)))
            .ToListAsync(ct).ConfigureAwait(false);

        return TypedResults.Ok(new InfobaseListResponse(items, total, p, ps));
    }

    // Занятость базы кластера для формы добавления/редактирования инфобазы. Возвращает
    // имя клиента-владельца, если база уже привязана (с исключением собственной базы при
    // редактировании через excludeId). Заменяет выгрузку всего списка баз на фронте (MLC-015).
    // 409 INFOBASE_ALREADY_ASSIGNED на create/update остаётся backstop'ом — это лишь UX-подсказка.
    internal static async Task<Ok<ClusterIdAvailabilityResponse>> ClusterIdAvailabilityAsync(
        AppDbContext db,
        [FromQuery] Guid clusterInfobaseId,
        [FromQuery] Guid? excludeId,
        CancellationToken ct)
    {
        var query = db.Infobases.AsNoTracking().Where(x => x.ClusterInfobaseId == clusterInfobaseId);
        if (excludeId is { } ex)
        {
            query = query.Where(x => x.Id != ex);
        }

        // Глобальная уникальность ClusterInfobaseId → совпадение не более одного; имя
        // владельца — имя единственного совпавшего клиента (null, если база свободна).
        var takenByTenantName = await query
            .Join(
                db.Tenants.AsNoTracking(),
                ib => ib.TenantId,
                t => t.Id,
                (ib, t) => t.Name)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return TypedResults.Ok(new ClusterIdAvailabilityResponse(takenByTenantName is not null, takenByTenantName));
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
        var normalizedDbName = (request.DatabaseName ?? string.Empty).Trim();

        var errors = ValidateInfobase(normalizedName, normalizedDbName, request.Status);
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
            DatabaseName = normalizedDbName,
            Status = request.Status,
            CreatedAt = now,
        };

        // Required-поля (SiteName/VirtualPath/PlatformVersion) тут же заполняет
        // ApplyPublicationFields — placeholder'ы лишь удовлетворяют инициализатор.
        var publication = new Publication
        {
            Id = Guid.NewGuid(),
            InfobaseId = infobase.Id,
            CreatedAt = now,
            SiteName = null!,
            VirtualPath = null!,
            PlatformVersion = null!,
        };
        ApplyPublicationFields(publication, request.Publication);

        // MLC-092: заведённая база перестаёт быть «нераспределённой» по определению —
        // её запись игнор-листа (если оператор раньше скрывал базу) удаляется тем же
        // SaveChanges, мусор в HiddenClusterInfobases не копим.
        var hiddenEntry = await db.HiddenClusterInfobases
            .FirstOrDefaultAsync(h => h.ClusterInfobaseId == request.ClusterInfobaseId, ct).ConfigureAwait(false);
        if (hiddenEntry is not null)
        {
            db.HiddenClusterInfobases.Remove(hiddenEntry);
        }

        // Инфобаза + публикация — один aggregate, попадают в БД одним SaveChanges.
        db.Infobases.Add(infobase);
        db.Publications.Add(publication);
        // MLC-004 — предварительные AnyAsync выше остаются happy-path'ом; на гонке двух
        // вставок их backstop — уникальные индексы. DbUpdateException мапим в тот же 409,
        // что и happy-path, вместо голого 500.
        var conflict = await db.SaveWithUniquenessBackstopAsync(ct,
            (UniqueIndexViolation.InfobaseTenantName, () => Problems.InfobaseNameDuplicateInTenant(normalizedName)),
            (UniqueIndexViolation.InfobaseClusterId, Problems.InfobaseAlreadyAssigned)).ConfigureAwait(false);
        if (conflict is not null)
        {
            return TypedResults.Conflict(conflict);
        }

        await httpContext.AuditAsync(audit, AuditActionType.InfobaseCreated,
            init => AuditDescriptions.InfobaseCreated(infobase.Name, init),
            infobase.TenantId, ct).ConfigureAwait(false);
        await httpContext.AuditAsync(audit, AuditActionType.PublicationCreated,
            init => AuditDescriptions.PublicationCreatedForInfobase(
                $"{publication.SiteName}{publication.VirtualPath}", infobase.Name, init),
            infobase.TenantId, ct).ConfigureAwait(false);

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
        var normalizedDbName = (request.DatabaseName ?? string.Empty).Trim();

        var errors = ValidateInfobase(normalizedName, normalizedDbName, request.Status);
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
        infobase.DatabaseName = normalizedDbName;
        infobase.Status = request.Status;
        infobase.UpdatedAt = now;

        ApplyPublicationFields(publication, request.Publication);
        publication.UpdatedAt = now;

        // MLC-004 — backstop на гонке (см. CreateAsync): нарушение уникального индекса
        // мапим в тот же 409, что и предварительные AnyAsync.
        var conflict = await db.SaveWithUniquenessBackstopAsync(ct,
            (UniqueIndexViolation.InfobaseTenantName, () => Problems.InfobaseNameDuplicateInTenant(normalizedName)),
            (UniqueIndexViolation.InfobaseClusterId, Problems.InfobaseAlreadyAssigned)).ConfigureAwait(false);
        if (conflict is not null)
        {
            return TypedResults.Conflict(conflict);
        }

        await httpContext.AuditAsync(audit, AuditActionType.InfobaseUpdated,
            init => AuditDescriptions.InfobaseUpdated(infobase.Name, init),
            infobase.TenantId, ct).ConfigureAwait(false);
        await httpContext.AuditAsync(audit, AuditActionType.PublicationUpdated,
            init => AuditDescriptions.PublicationUpdatedForInfobase(
                $"{publication.SiteName}{publication.VirtualPath}", infobase.Name, init),
            infobase.TenantId, ct).ConfigureAwait(false);

        return TypedResults.Ok(infobase.ToDetailResponse(publication));
    }

    // Перенос базы другому клиенту — отдельная операция, а не правка через PUT
    // (в форме редактирования клиент заблокирован). Имя инфобазы уникально внутри
    // клиента, поэтому при коллизии у целевого клиента отвечаем 409.
    internal static async Task<Results<Ok<InfobaseDetailResponse>, NotFound, Conflict<ProblemDetails>>> ReassignAsync(
        Guid id,
        [FromBody] ReassignInfobaseRequest request,
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

        var target = await db.Tenants.FirstOrDefaultAsync(t => t.Id == request.TargetTenantId, ct).ConfigureAwait(false);
        if (target is null)
        {
            return TypedResults.NotFound();
        }

        // Перенос на того же клиента — no-op, отдаём текущее состояние.
        if (infobase.TenantId == target.Id)
        {
            return TypedResults.Ok(infobase.ToDetailResponse(publication));
        }

        if (await db.Infobases.AnyAsync(x => x.TenantId == target.Id && x.Name == infobase.Name, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.InfobaseNameTakenInTarget(infobase.Name));
        }

        var sourceTenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == infobase.TenantId, ct).ConfigureAwait(false);
        var sourceName = sourceTenant?.Name ?? infobase.TenantId.ToString();

        infobase.TenantId = target.Id;
        infobase.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        // MLC-004 — backstop: одновременный перенос/создание одноимённой базы у целевого
        // клиента нарушит IX_Infobases_TenantId_Name. В контексте переноса это 409
        // INFOBASE_NAME_TAKEN_IN_TARGET (тот же индекс, другой ProblemCodes, чем в create).
        var conflict = await db.SaveWithUniquenessBackstopAsync(ct,
            (UniqueIndexViolation.InfobaseTenantName, () => Problems.InfobaseNameTakenInTarget(infobase.Name))).ConfigureAwait(false);
        if (conflict is not null)
        {
            return TypedResults.Conflict(conflict);
        }

        await httpContext.AuditAsync(audit, AuditActionType.InfobaseReassigned,
            init => AuditDescriptions.InfobaseReassigned(infobase.Name, sourceName, target.Name, init),
            target.Id, ct).ConfigureAwait(false);

        return TypedResults.Ok(infobase.ToDetailResponse(publication));
    }

    // MLC-113 (UX-43): необязательное снятие IIS-публикации при удалении инфобазы.
    // unpublishFromIis=true → СНАЧАЛА webinst -delete; при сбое — 409 и НИЧЕГО не
    // удаляем (защита от молчаливого сиротства публикации в IIS — главная цель UX-43).
    // unpublishFromIis=false → поведение строго прежнее (webinst не зовём).
    internal static async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>>> DeleteAsync(
        Guid id,
        AppDbContext db,
        IAuditLogger audit,
        IWebinstPublisher webinst,
        HttpContext httpContext,
        CancellationToken ct,
        [FromQuery] bool unpublishFromIis = false)
    {
        var infobase = await db.Infobases.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (infobase is null)
        {
            return TypedResults.NotFound();
        }

        var publication = await db.Publications.FirstOrDefaultAsync(p => p.InfobaseId == id, ct).ConfigureAwait(false);

        var infobaseName = infobase.Name;
        var tenantId = infobase.TenantId;
        var publicationLabel = publication is null
            ? null
            : $"{publication.SiteName}{publication.VirtualPath}";

        // Снятие из IIS ДО удаления строк: при сбое webinst откатываемся в 409, оставляя
        // запись на месте — оператор снимет галочку и удалит без очистки IIS либо повторит.
        if (unpublishFromIis && publication is not null)
        {
            var unpublish = await webinst.UnpublishAsync(publication, infobase, ct).ConfigureAwait(false);
            if (!unpublish.Success)
            {
                return TypedResults.Conflict(Problems.UnpublishFailed(
                    unpublish.ErrorDetail ?? "Не удалось снять публикацию инфобазы.", httpContext.TraceIdentifier));
            }

            await httpContext.AuditAsync(audit, AuditActionType.PublicationUnpublished,
                init => AuditDescriptions.PublicationUnpublished(publicationLabel!, init),
                tenantId, ct).ConfigureAwait(false);
        }

        // Аудит пишем ДО удаления — TenantId ещё валиден, FK не нарушается.
        if (publication is not null)
        {
            await httpContext.AuditAsync(audit, AuditActionType.PublicationDeleted,
                init => AuditDescriptions.PublicationDeletedWithInfobase(publicationLabel!, infobaseName, init),
                tenantId, ct).ConfigureAwait(false);
        }
        await httpContext.AuditAsync(audit, AuditActionType.InfobaseDeleted,
            init => AuditDescriptions.InfobaseDeleted(infobaseName, init),
            tenantId, ct).ConfigureAwait(false);

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
        string normalizedDbName,
        InfobaseStatus status)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            errors[nameof(CreateInfobaseRequest.Name)] = ["Название инфобазы не может быть пустым."];
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

    // MLC-022 — публикационная валидация централизована в InfobaseValidationRules.
    // Вложенная публикация инфобазы префиксует ключи полей «Publication.».
    private static void AppendPublicationErrors(
        Dictionary<string, string[]> errors,
        CreatePublicationRequest publication)
    {
        InfobaseValidationRules.AppendPublicationFieldErrors(errors, $"{nameof(CreateInfobaseRequest.Publication)}.",
            publication.SiteName, publication.VirtualPath, publication.PlatformVersion, publication.PhysicalPathOverride);
    }

    private static void AppendPublicationErrors(
        Dictionary<string, string[]> errors,
        UpdatePublicationRequest publication)
    {
        InfobaseValidationRules.AppendPublicationFieldErrors(errors, $"{nameof(CreateInfobaseRequest.Publication)}.",
            publication.SiteName, publication.VirtualPath, publication.PlatformVersion, publication.PhysicalPathOverride);
    }

    // MLC-029 — единый маппинг полей публикации request→entity для Create и Update.
    // Закрывает общие поля (с тем же trim + null-нормализацией PhysicalPathOverride);
    // Id/InfobaseId/CreatedAt (Create) и UpdatedAt (Update) остаются за вызывающим.
    // Create/UpdatePublicationRequest — разные типы, поэтому, как и у
    // AppendPublicationErrors, две тонкие перегрузки-адаптера над общим ядром.
    private static void ApplyPublicationFields(
        Publication target,
        string siteName,
        string virtualPath,
        string platformVersion,
        string? physicalPathOverride)
    {
        target.SiteName = siteName.Trim();
        target.VirtualPath = virtualPath.Trim();
        target.PlatformVersion = platformVersion.Trim();
        target.PhysicalPathOverride = string.IsNullOrWhiteSpace(physicalPathOverride)
            ? null
            : physicalPathOverride.Trim().TrimEnd('\\', '/');
    }

    private static void ApplyPublicationFields(Publication target, CreatePublicationRequest request) =>
        ApplyPublicationFields(target, request.SiteName, request.VirtualPath, request.PlatformVersion,
            request.PhysicalPathOverride);

    private static void ApplyPublicationFields(Publication target, UpdatePublicationRequest request) =>
        ApplyPublicationFields(target, request.SiteName, request.VirtualPath, request.PlatformVersion,
            request.PhysicalPathOverride);
}
