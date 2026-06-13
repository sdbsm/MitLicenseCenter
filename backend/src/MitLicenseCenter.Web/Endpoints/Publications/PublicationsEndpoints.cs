using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

// Publications в Stage 2 управляются в составе aggregate'а Infobase (см.
// InfobasesEndpoints). Этот модуль — прямое редактирование полей публикации плюс
// операции MLC-045:
//   POST /{id}/check           — read-only проверка факта публикации в IIS.
//   POST /{id}/publish         — (пере)публикация через webinst.exe.
//   POST /{id}/change-platform — правка пути к wsisapi.dll в web.config под новую версию.
public static partial class PublicationsEndpoints
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

        // MLC-045: операции публикации.
        group.MapPost("/{id:guid}/check", CheckAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/publish", PublishAsync).RequireAuthorization(Roles.Admin);
        // MLC-113: снятие IIS-публикации через webinst -delete (UX-43).
        group.MapPost("/{id:guid}/unpublish", UnpublishAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/change-platform", ChangePlatformAsync).RequireAuthorization(Roles.Admin);
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

    // internal (не private) ради Stage-2 теста валидации BE-09 (MLC-120) — зеркаль
    // остальных операционных handler'ов этого модуля (Check/Publish/…). Поведение и
    // регистрация маршрута не меняются.
    internal static async Task<Results<Ok<PublicationResponse>, NotFound, ValidationProblem>> UpdateAsync(
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
        InfobaseValidationRules.AppendPublicationFieldErrors(
            errors, string.Empty, siteName, virtualPath, platformVersion, request.PhysicalPathOverride);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var infobase = await db.Infobases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == publication.InfobaseId, ct).ConfigureAwait(false);

        publication.SiteName = siteName;
        publication.VirtualPath = virtualPath;
        publication.PlatformVersion = platformVersion;
        publication.PhysicalPathOverride = string.IsNullOrWhiteSpace(request.PhysicalPathOverride)
            ? null
            : request.PhysicalPathOverride.Trim().TrimEnd('\\', '/');
        publication.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.PublicationUpdated,
            init => AuditDescriptions.PublicationUpdated(
                $"{publication.SiteName}{publication.VirtualPath}", init),
            infobase?.TenantId, ct).ConfigureAwait(false);

        return TypedResults.Ok(publication.ToResponse());
    }

    // Read-only проверка факта публикации в IIS (MLC-045). Синхронно читает состояние
    // и обновляет LastCheck*. Ничего не меняет в IIS, аудит не пишет.
    internal static async Task<Results<Ok<PublicationStatusResponse>, NotFound>> CheckAsync(
        Guid id,
        AppDbContext db,
        IPublicationStatusJob statusJob,
        CancellationToken ct)
    {
        var exists = await db.Publications.AsNoTracking().AnyAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (!exists)
        {
            return TypedResults.NotFound();
        }

        await statusJob.RefreshOneAsync(id, ct).ConfigureAwait(false);

        var status = await db.Publications
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PublicationStatusResponse(x.LastCheckStatus, x.LastCheckAt, x.LastCheckDetails))
            .FirstAsync(ct)
            .ConfigureAwait(false);
        return TypedResults.Ok(status);
    }

    // (Пере)публикация инфобазы через webinst.exe (MLC-045). Гейт: если публикация
    // сделана не панелью (Source ≠ Webinst) и сейчас опубликована — требуем Confirm,
    // т.к. webinst перезатрёт ручную конфигурацию.
    internal static async Task<Results<Ok<PublicationStatusResponse>, NotFound, Conflict<ProblemDetails>>> PublishAsync(
        Guid id,
        [FromBody] PublishPublicationRequest request,
        AppDbContext db,
        IWebinstPublisher webinst,
        IPublicationStatusJob statusJob,
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

        var infobase = await db.Infobases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == publication.InfobaseId, ct).ConfigureAwait(false);
        if (infobase is null)
        {
            return TypedResults.NotFound();
        }

        // Гейт перезатирания: чужая (не webinst) и уже опубликованная публикация —
        // только с явным подтверждением оператора.
        if (publication.Source != PublicationSource.Webinst
            && publication.LastCheckStatus == PublicationPublishStatus.Published
            && !request.Confirm)
        {
            return TypedResults.Conflict(Problems.PublishConfirmRequired());
        }

        var result = await webinst.PublishAsync(publication, infobase, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            // Адаптер уже залогировал сырой вывод webinst; detail санитизирован.
            return TypedResults.Conflict(Problems.PublishFailed(
                result.ErrorDetail ?? "Не удалось опубликовать инфобазу.", httpContext.TraceIdentifier));
        }

        publication.Source = PublicationSource.Webinst;
        publication.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Обновляем фактический статус (в том же scope сущность трекается → мутируется).
        await statusJob.RefreshOneAsync(id, ct).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.PublicationPublished,
            init => AuditDescriptions.PublicationPublished(
                $"{publication.SiteName}{publication.VirtualPath}", init),
            infobase.TenantId, ct).ConfigureAwait(false);

        var status = await db.Publications
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PublicationStatusResponse(x.LastCheckStatus, x.LastCheckAt, x.LastCheckDetails))
            .FirstAsync(ct)
            .ConfigureAwait(false);
        return TypedResults.Ok(status);
    }

    // Снятие IIS-публикации через webinst -delete (MLC-113, UX-43). Зеркаль PublishAsync,
    // но без confirm-гейта: разрушительность подтверждается токеном в UI. Source НЕ
    // сбрасываем (метаданные происхождения); гейт перезаписи смотрит на Published-статус,
    // который после снятия станет NotPublished. При сбое webinst — 409 без аудита.
    internal static async Task<Results<Ok<PublicationStatusResponse>, NotFound, Conflict<ProblemDetails>>> UnpublishAsync(
        Guid id,
        AppDbContext db,
        IWebinstPublisher webinst,
        IPublicationStatusJob statusJob,
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

        var infobase = await db.Infobases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == publication.InfobaseId, ct).ConfigureAwait(false);
        if (infobase is null)
        {
            return TypedResults.NotFound();
        }

        var result = await webinst.UnpublishAsync(publication, infobase, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            // Адаптер уже залогировал сырой вывод webinst; detail санитизирован.
            return TypedResults.Conflict(Problems.UnpublishFailed(
                result.ErrorDetail ?? "Не удалось снять публикацию инфобазы.", httpContext.TraceIdentifier));
        }

        publication.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Обновляем фактический статус — после снятия станет NotPublished.
        await statusJob.RefreshOneAsync(id, ct).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.PublicationUnpublished,
            init => AuditDescriptions.PublicationUnpublished(
                $"{publication.SiteName}{publication.VirtualPath}", init),
            infobase.TenantId, ct).ConfigureAwait(false);

        var status = await db.Publications
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PublicationStatusResponse(x.LastCheckStatus, x.LastCheckAt, x.LastCheckDetails))
            .FirstAsync(ct)
            .ConfigureAwait(false);
        return TypedResults.Ok(status);
    }

    // Смена платформы (MLC-045): правит путь к wsisapi.dll в web.config под новую
    // версию. default.vrd не трогается. При IIS-исключении — 409, аудит не пишется.
    internal static async Task<Results<Ok<PublicationStatusResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> ChangePlatformAsync(
        Guid id,
        [FromBody] ChangePlatformRequest request,
        AppDbContext db,
        IIisPublishingService iis,
        IPlatformVersionDiscovery platforms,
        IPublicationStatusJob statusJob,
        IAuditLogger audit,
        HttpContext httpContext,
        TimeProvider clock,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var publication = await db.Publications.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (publication is null)
        {
            return TypedResults.NotFound();
        }

        var newVersion = (request.PlatformVersion ?? string.Empty).Trim();
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!InfobaseValidationRules.IsValidPlatformVersion(newVersion))
        {
            errors["PlatformVersion"] = ["Версия должна состоять из четырёх числовых сегментов, например «8.3.23.1865» или «8.5.1.1302»."];
        }
        else
        {
            // Если можем просканировать установленные версии — целевая должна быть среди них
            // (иначе путь к wsisapi.dll новой версии не будет существовать и приложение сломается).
            var installed = platforms.FindPlatformVersions();
            if (installed.Count > 0 && !installed.Any(v => string.Equals(v.Version, newVersion, StringComparison.Ordinal)))
            {
                errors["PlatformVersion"] = [$"Версия платформы {newVersion} не установлена на сервере."];
            }
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var previousVersion = publication.PlatformVersion;
        var correlationId = httpContext.TraceIdentifier;
        try
        {
            await iis.ChangePlatformAsync(publication, newVersion, ct).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogPlatformAccessDenied(loggerFactory.CreateLogger(typeof(PublicationsEndpoints).FullName!), id, correlationId, ex);
            return TypedResults.Conflict(Problems.IisAccessDenied(correlationId));
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            LogPlatformChangeFailed(loggerFactory.CreateLogger(typeof(PublicationsEndpoints).FullName!), id, correlationId, ex);
            return TypedResults.Conflict(Problems.IisReconcileFailed(correlationId));
        }
        catch (IOException ex)
        {
            LogPlatformChangeFailed(loggerFactory.CreateLogger(typeof(PublicationsEndpoints).FullName!), id, correlationId, ex);
            return TypedResults.Conflict(Problems.IisReconcileFailed(correlationId));
        }
        catch (InvalidOperationException ex)
        {
            LogPlatformChangeFailed(loggerFactory.CreateLogger(typeof(PublicationsEndpoints).FullName!), id, correlationId, ex);
            return TypedResults.Conflict(Problems.IisReconcileFailed(correlationId));
        }

        publication.PlatformVersion = newVersion;
        publication.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await statusJob.RefreshOneAsync(id, ct).ConfigureAwait(false);

        var tenantId = await db.Infobases
            .AsNoTracking()
            .Where(i => i.Id == publication.InfobaseId)
            .Select(i => (Guid?)i.TenantId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.PublicationPlatformChanged,
            init => AuditDescriptions.PublicationPlatformChanged(
                $"{publication.SiteName}{publication.VirtualPath}", previousVersion, newVersion, init),
            tenantId, ct).ConfigureAwait(false);

        var status = await db.Publications
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PublicationStatusResponse(x.LastCheckStatus, x.LastCheckAt, x.LastCheckDetails))
            .FirstAsync(ct)
            .ConfigureAwait(false);
        return TypedResults.Ok(status);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Change-platform {PublicationId}: нет доступа к IIS/файлам публикации (correlationId={CorrelationId}).")]
    private static partial void LogPlatformAccessDenied(ILogger logger, Guid publicationId, string correlationId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Change-platform {PublicationId}: смена версии платформы не удалась (correlationId={CorrelationId}).")]
    private static partial void LogPlatformChangeFailed(ILogger logger, Guid publicationId, string correlationId, Exception ex);
}
