using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-047 (ADR-24): управление жизненным циклом IIS из веб-панели. Server-scope
// операции (пул/сайт/iisreset), поэтому отдельная группа /api/v1/iis, а не
// /publications/{id}/... Анти-коррупционная граница (ADR-20): всё через
// IIisLifecycleService, без прямого ServerManager/Process в Web.
//
//   GET  /iis/application-pools            — список пулов с состоянием (Viewer).
//   GET  /iis/sites                        — список сайтов с состоянием (Viewer).
//   POST /iis/application-pools/recycle     — recycle пула (Admin, Confirm).
//   POST /iis/application-pools/start|stop  — start/stop пула (Admin).
//   POST /iis/sites/start|stop|restart      — управление сайтом (Admin).
//   POST /iis/reset                         — полный iisreset (Admin, Confirm).
public static partial class IisEndpoints
{
    public static void MapIisEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/iis")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Iis");

        group.MapGet("/server", GetServerStatusAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/application-pools", ListApplicationPoolsAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/sites", ListSitesAsync).RequireAuthorization(Roles.Viewer);

        group.MapPost("/application-pools/recycle", RecyclePoolAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/application-pools/start", StartPoolAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/application-pools/stop", StopPoolAsync).RequireAuthorization(Roles.Admin);

        group.MapPost("/sites/start", StartSiteAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/sites/stop", StopSiteAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/sites/restart", RestartSiteAsync).RequireAuthorization(Roles.Admin);

        group.MapPost("/reset", ResetIisAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/stop", StopIisAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/start", StartIisAsync).RequireAuthorization(Roles.Admin);
    }

    // ── Discovery (read-only) ────────────────────────────────────────────────────────

    internal static async Task<Ok<IisServerStatusResponse>> GetServerStatusAsync(
        IIisLifecycleService iis,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        try
        {
            var state = await iis.GetServerStateAsync(ct).ConfigureAwait(false);
            return TypedResults.Ok(new IisServerStatusResponse(state.ToString(), Available: true, Error: null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogServerStatusFailed(loggerFactory.CreateLogger(typeof(IisEndpoints).FullName!), ex);
            return TypedResults.Ok(new IisServerStatusResponse(
                "Unknown",
                Available: false,
                Error: "Не удалось получить состояние службы IIS (W3SVC). Проверьте доступность веб-сервера и права службы."));
        }
    }

    internal static async Task<Ok<DiscoveryResponse<IisAppPoolDto>>> ListApplicationPoolsAsync(
        IIisLifecycleService iis,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        try
        {
            var pools = await iis.ListApplicationPoolsAsync(ct).ConfigureAwait(false);
            var items = pools.Select(p => new IisAppPoolDto(p.Name, p.State.ToString())).ToList();
            return TypedResults.Ok(new DiscoveryResponse<IisAppPoolDto>(items, Available: true, Error: null));
        }
        // MLC-009: отмену не выдаём за «ошибку discovery» — пробрасываем.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPoolsDiscoveryFailed(loggerFactory.CreateLogger(typeof(IisEndpoints).FullName!), ex);
            return TypedResults.Ok(new DiscoveryResponse<IisAppPoolDto>(
                Array.Empty<IisAppPoolDto>(),
                Available: false,
                Error: "Не удалось получить список пулов приложений IIS. Проверьте доступность веб-сервера и права службы."));
        }
    }

    internal static async Task<Ok<DiscoveryResponse<IisSiteStateDto>>> ListSitesAsync(
        IIisLifecycleService iis,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        try
        {
            var sites = await iis.ListSitesAsync(ct).ConfigureAwait(false);
            var items = sites.Select(s => new IisSiteStateDto(s.SiteName, s.State.ToString())).ToList();
            return TypedResults.Ok(new DiscoveryResponse<IisSiteStateDto>(items, Available: true, Error: null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSitesDiscoveryFailed(loggerFactory.CreateLogger(typeof(IisEndpoints).FullName!), ex);
            return TypedResults.Ok(new DiscoveryResponse<IisSiteStateDto>(
                Array.Empty<IisSiteStateDto>(),
                Available: false,
                Error: "Не удалось получить список сайтов IIS. Проверьте доступность веб-сервера и права службы."));
        }
    }

    // ── Пул приложений ───────────────────────────────────────────────────────────────

    internal static Task<Results<Ok<IisOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> RecyclePoolAsync(
        [FromBody] IisTargetRequest request,
        IIisLifecycleService iis,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        // Серверный confirm-гейт (защита от случайного клика помимо токена в UI).
        if (!request.Confirm)
        {
            return Task.FromResult<Results<Ok<IisOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>>(
                TypedResults.Conflict(Problems.IisConfirmRequired()));
        }

        return ExecuteTargetAsync(
            request.Name, iis.RecycleApplicationPoolAsync,
            AuditActionType.IisApplicationPoolRecycled, AuditDescriptions.IisApplicationPoolRecycled,
            httpContext, audit, loggerFactory, ct);
    }

    internal static Task<Results<Ok<IisOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> StartPoolAsync(
        [FromBody] IisTargetRequest request,
        IIisLifecycleService iis,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteTargetAsync(
            request.Name, iis.StartApplicationPoolAsync,
            AuditActionType.IisApplicationPoolStarted, AuditDescriptions.IisApplicationPoolStarted,
            httpContext, audit, loggerFactory, ct);

    internal static Task<Results<Ok<IisOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> StopPoolAsync(
        [FromBody] IisTargetRequest request,
        IIisLifecycleService iis,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteTargetAsync(
            request.Name, iis.StopApplicationPoolAsync,
            AuditActionType.IisApplicationPoolStopped, AuditDescriptions.IisApplicationPoolStopped,
            httpContext, audit, loggerFactory, ct);

    // ── Сайт ───────────────────────────────────────────────────────────────────────

    internal static Task<Results<Ok<IisOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> StartSiteAsync(
        [FromBody] IisTargetRequest request,
        IIisLifecycleService iis,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteTargetAsync(
            request.Name, iis.StartSiteAsync,
            AuditActionType.IisSiteStarted, AuditDescriptions.IisSiteStarted,
            httpContext, audit, loggerFactory, ct);

    internal static Task<Results<Ok<IisOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> StopSiteAsync(
        [FromBody] IisTargetRequest request,
        IIisLifecycleService iis,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteTargetAsync(
            request.Name, iis.StopSiteAsync,
            AuditActionType.IisSiteStopped, AuditDescriptions.IisSiteStopped,
            httpContext, audit, loggerFactory, ct);

    internal static Task<Results<Ok<IisOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> RestartSiteAsync(
        [FromBody] IisTargetRequest request,
        IIisLifecycleService iis,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteTargetAsync(
            request.Name, iis.RestartSiteAsync,
            AuditActionType.IisSiteRestarted, AuditDescriptions.IisSiteRestarted,
            httpContext, audit, loggerFactory, ct);

    // ── Полный перезапуск (iisreset) ─────────────────────────────────────────────────

    // iisreset (restart) — разрушительно: серверный Confirm-гейт.
    internal static Task<Results<Ok, Conflict<ProblemDetails>>> ResetIisAsync(
        [FromBody] IisResetRequest request,
        IIisLifecycleService iis,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!request.Confirm)
        {
            return Task.FromResult<Results<Ok, Conflict<ProblemDetails>>>(
                TypedResults.Conflict(Problems.IisConfirmRequired()));
        }
        return ExecuteServerOpAsync(
            iis.RestartIisAsync, "iisreset", AuditActionType.IisReset, AuditDescriptions.IisReset,
            httpContext, audit, loggerFactory, ct);
    }

    // iisreset /stop — разрушительно (роняет весь IIS): серверный Confirm-гейт.
    internal static Task<Results<Ok, Conflict<ProblemDetails>>> StopIisAsync(
        [FromBody] IisResetRequest request,
        IIisLifecycleService iis,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!request.Confirm)
        {
            return Task.FromResult<Results<Ok, Conflict<ProblemDetails>>>(
                TypedResults.Conflict(Problems.IisConfirmRequired()));
        }
        return ExecuteServerOpAsync(
            iis.StopIisAsync, "iisreset /stop", AuditActionType.IisStopped, AuditDescriptions.IisStopped,
            httpContext, audit, loggerFactory, ct);
    }

    // iisreset /start — восстановление (не разрушительно), без Confirm-гейта.
    internal static Task<Results<Ok, Conflict<ProblemDetails>>> StartIisAsync(
        IIisLifecycleService iis,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteServerOpAsync(
            iis.StartIisAsync, "iisreset /start", AuditActionType.IisStarted, AuditDescriptions.IisStarted,
            httpContext, audit, loggerFactory, ct);

    // Общий раннер server-scope операции (iisreset restart/stop/start): маппинг
    // исключений в 409, аудит при успехе (tenantId=null). Нет цели → нет 404.
    private static async Task<Results<Ok, Conflict<ProblemDetails>>> ExecuteServerOpAsync(
        Func<CancellationToken, Task> operation,
        string opLabel,
        AuditActionType action,
        Func<string, string> describe,
        HttpContext httpContext,
        IAuditLogger audit,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var correlationId = httpContext.TraceIdentifier;
        try
        {
            await operation(ct).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogIisOpAccessDenied(loggerFactory.CreateLogger(typeof(IisEndpoints).FullName!), opLabel, correlationId, ex);
            return TypedResults.Conflict(Problems.IisAccessDenied(correlationId));
        }
        catch (Exception ex) when (IsIisOperationFailure(ex))
        {
            LogIisOpFailed(loggerFactory.CreateLogger(typeof(IisEndpoints).FullName!), opLabel, correlationId, ex);
            return TypedResults.Conflict(Problems.IisOperationFailed(correlationId));
        }

        await httpContext.AuditAsync(audit, action, describe, tenantId: null, ct).ConfigureAwait(false);
        return TypedResults.Ok();
    }

    // Общий раннер операции над именованной целью (пул/сайт): trim+валидация имени,
    // маппинг исключений в 404/409, аудит при успехе (tenantId=null — server-scope).
    private static async Task<Results<Ok<IisOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> ExecuteTargetAsync(
        string? rawName,
        Func<string, CancellationToken, Task<IisObjectState>> operation,
        AuditActionType action,
        Func<string, string, string> describe,
        HttpContext httpContext,
        IAuditLogger audit,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var name = (rawName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Name"] = ["Не указано имя пула/сайта."],
            });
        }

        var correlationId = httpContext.TraceIdentifier;
        IisObjectState state;
        try
        {
            state = await operation(name, ct).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            LogIisOpAccessDenied(loggerFactory.CreateLogger(typeof(IisEndpoints).FullName!), name, correlationId, ex);
            return TypedResults.Conflict(Problems.IisAccessDenied(correlationId));
        }
        catch (Exception ex) when (IsIisOperationFailure(ex))
        {
            LogIisOpFailed(loggerFactory.CreateLogger(typeof(IisEndpoints).FullName!), name, correlationId, ex);
            return TypedResults.Conflict(Problems.IisOperationFailed(correlationId));
        }

        await httpContext.AuditAsync(audit, action, init => describe(name, init), tenantId: null, ct).ConfigureAwait(false);
        return TypedResults.Ok(new IisOperationResponse(name, state.ToString()));
    }

    // COM/IO/таймаут/ненулевой exit iisreset — наружу 409 IIS_OPERATION_FAILED.
    private static bool IsIisOperationFailure(Exception ex) =>
        ex is System.Runtime.InteropServices.COMException
            or InvalidOperationException
            or IOException
            or TimeoutException;
}

public static partial class IisEndpoints
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "IIS discovery: не удалось получить состояние службы W3SVC.")]
    private static partial void LogServerStatusFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "IIS discovery: не удалось получить список пулов приложений.")]
    private static partial void LogPoolsDiscoveryFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "IIS discovery: не удалось получить список сайтов.")]
    private static partial void LogSitesDiscoveryFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "IIS-операция над «{Target}»: нет доступа (correlationId={CorrelationId}).")]
    private static partial void LogIisOpAccessDenied(ILogger logger, string target, string correlationId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "IIS-операция над «{Target}» не удалась (correlationId={CorrelationId}).")]
    private static partial void LogIisOpFailed(ILogger logger, string target, string correlationId, Exception ex);
}
