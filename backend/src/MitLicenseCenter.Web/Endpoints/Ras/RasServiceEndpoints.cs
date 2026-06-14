using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

// Управление локальной службой RAS из веб-панели (MLC-159, ADR-47). Admin-only,
// server-scope. Хост фиксирован localhost (single-host, ADR-28). Анти-коррупционная
// граница (ADR-20): всё через IRasServiceManager, без прямого sc.exe/Process в Web.
//
//   GET  /ras-service/status            — диагностика 4 состояний + предпросмотр sc (Admin).
//   POST /ras-service/register          — sc create новой службы (Admin, аудит 600).
//   POST /ras-service/update            — перерегистрация под платформу/порт (Admin, аудит 601).
//   POST /ras-service/start             — запуск остановленной службы (Admin, аудит 602).
public static partial class RasServiceEndpoints
{
    public static void MapRasServiceEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/ras-service")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("RasService")
            .RequireAuthorization(Roles.Admin);

        group.MapGet("/status", GetStatusAsync);
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/update", UpdateAsync);
        group.MapPost("/start", StartAsync);
    }

    // Диагностика. Сама по себе мутаций не делает; перечисление служб через sc может
    // упасть (нет прав / sc недоступен) → 409 с санитизированным текстом.
    internal static async Task<Results<Ok<RasServiceStatusResponse>, Conflict<ProblemDetails>>> GetStatusAsync(
        [FromServices] IRasServiceManager manager,
        HttpContext httpContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var correlationId = httpContext.TraceIdentifier;
        try
        {
            var d = await manager.DiagnoseAsync(ct).ConfigureAwait(false);
            return TypedResults.Ok(new RasServiceStatusResponse(
                State: d.State.ToString(),
                Service: ToDto(d.Service),
                Target: ToDto(d.Target),
                CommandPreview: d.CommandPreview,
                TargetReady: d.TargetReady,
                Issue: d.Issue));
        }
        catch (RasServiceOperationException ex)
        {
            LogRasStatusFailed(loggerFactory.CreateLogger(typeof(RasServiceEndpoints).FullName!), correlationId, ex);
            return TypedResults.Conflict(Problems.RasServiceOperationFailed(ex.Message, correlationId));
        }
    }

    internal static Task<Results<Ok<RasServiceOperationResponse>, Conflict<ProblemDetails>>> RegisterAsync(
        [FromServices] IRasServiceManager manager,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteAsync(
            RasServiceOperation.Register, manager, audit, httpContext, loggerFactory, ct);

    internal static Task<Results<Ok<RasServiceOperationResponse>, Conflict<ProblemDetails>>> UpdateAsync(
        [FromServices] IRasServiceManager manager,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteAsync(
            RasServiceOperation.Update, manager, audit, httpContext, loggerFactory, ct);

    internal static Task<Results<Ok<RasServiceOperationResponse>, Conflict<ProblemDetails>>> StartAsync(
        [FromServices] IRasServiceManager manager,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteAsync(
            RasServiceOperation.Start, manager, audit, httpContext, loggerFactory, ct);

    // Общий раннер: операция через менеджер, маппинг RasServiceOperationException → 409,
    // аудит при успехе (tenantId=null — server-scope; секреты в описание не попадают).
    private static async Task<Results<Ok<RasServiceOperationResponse>, Conflict<ProblemDetails>>> ExecuteAsync(
        RasServiceOperation operation,
        IRasServiceManager manager,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var correlationId = httpContext.TraceIdentifier;
        RasServiceOperationResult result;
        try
        {
            result = operation switch
            {
                RasServiceOperation.Register => await manager.RegisterAsync(ct).ConfigureAwait(false),
                RasServiceOperation.Update => await manager.UpdateAsync(ct).ConfigureAwait(false),
                RasServiceOperation.Start => await manager.StartAsync(ct).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
        }
        catch (RasServiceOperationException ex)
        {
            LogRasOpFailed(loggerFactory.CreateLogger(typeof(RasServiceEndpoints).FullName!), operation.ToString(), correlationId, ex);
            return TypedResults.Conflict(Problems.RasServiceOperationFailed(ex.Message, correlationId));
        }

        var platform = result.PlatformVersion ?? "—";
        var port = result.Port ?? "—";
        var (action, describe) = operation switch
        {
            RasServiceOperation.Register => (
                AuditActionType.RasServiceRegistered,
                (Func<string, string>)(init => AuditDescriptions.RasServiceRegistered(result.ServiceName, platform, port, init))),
            RasServiceOperation.Update => (
                AuditActionType.RasServiceUpdated,
                init => AuditDescriptions.RasServiceUpdated(result.ServiceName, platform, port, init)),
            _ => (
                AuditActionType.RasServiceStarted,
                init => AuditDescriptions.RasServiceStarted(result.ServiceName, init)),
        };

        await httpContext.AuditAsync(audit, action, describe, tenantId: null, ct).ConfigureAwait(false);
        return TypedResults.Ok(new RasServiceOperationResponse(result.State.ToString(), result.ServiceName));
    }

    private static RasServiceInfoDto? ToDto(DiscoveredRasService? s)
        => s is null
            ? null
            : new RasServiceInfoDto(s.ServiceName, s.IsRunning, s.BinPath, s.PlatformVersion, s.Port);

    private static RasServiceTargetDto? ToDto(RasServiceTarget? t)
        => t is null
            ? null
            : new RasServiceTargetDto(t.RasExePath, t.PlatformVersion, t.Port, t.AgentAddress);
}

public static partial class RasServiceEndpoints
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "RAS-служба: диагностика не удалась (correlationId={CorrelationId}).")]
    private static partial void LogRasStatusFailed(ILogger logger, string correlationId, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "RAS-служба: операция «{Operation}» не удалась (correlationId={CorrelationId}).")]
    private static partial void LogRasOpFailed(ILogger logger, string operation, string correlationId, Exception ex);
}
