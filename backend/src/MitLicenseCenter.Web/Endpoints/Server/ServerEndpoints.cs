using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Maintenance;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

// Раздел «Сервер» (MLC-213, ADR-54/55): сводный статус служб стека узла + управление
// ТОЛЬКО сервером 1С (ragent). RAS/SQL/IIS в статусе — только наблюдение; их управление
// НЕ дублируется (остаётся в /ras-service/* и /api/v1/iis/*, SQL — без управления, ADR-54).
// Анти-коррупционная граница (ADR-20): всё через IServerStatusProvider /
// IWindowsServiceController, без прямого sc.exe/реестра/Process в Web. Single-host (ADR-28).
//
//   GET  /server/status                 — сводный статус стека (Viewer); деградация флагами.
//   GET  /server/maintenance/backups    — свежесть бэкапов баз (Viewer); деградация статусом.
//   GET  /server/maintenance/plans      — планы обслуживания SQL (Viewer); деградация статусом.
//   POST /server/onec/start             — запуск службы сервера 1С (Admin).
//   POST /server/onec/stop              — остановка (Admin, Confirm-гейт).
//   POST /server/onec/restart           — перезапуск (Admin, Confirm-гейт).
public static partial class ServerEndpoints
{
    public static void MapServerEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/server")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Server");

        group.MapGet("/status", GetStatusAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/maintenance/backups", GetBackupFreshnessAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/maintenance/plans", GetMaintenancePlansAsync).RequireAuthorization(Roles.Viewer);

        group.MapPost("/onec/start", StartOneCServerAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/onec/stop", StopOneCServerAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/onec/restart", RestartOneCServerAsync).RequireAuthorization(Roles.Admin);
    }

    // ── Статус (read-only) ──────────────────────────────────────────────────────────────

    // ВСЕГДА Ok: деградация адаптеров уже отражена внутри провайдера флагами Available/Error
    // (провайдер never-throws на сбое источника), эндпоинт не 500-ит.
    internal static async Task<Ok<ServerStatusResponse>> GetStatusAsync(
        [FromServices] IServerStatusProvider provider,
        CancellationToken ct)
    {
        var snapshot = await provider.GetStatusAsync(ct).ConfigureAwait(false);
        return TypedResults.Ok(ToResponse(snapshot));
    }

    // Вкладка «Обслуживание» (MLC-216): свежесть резервных копий баз (live-read backupset).
    // ВСЕГДА Ok: проба never-throws, деградация (нет прав на msdb.dbo.backupset / SQL недоступен)
    // отражена статусом снимка (PermissionDenied/Unavailable), эндпоинт не 500-ит.
    internal static async Task<Ok<BackupFreshnessResponse>> GetBackupFreshnessAsync(
        [FromServices] IMaintenanceProbe probe,
        CancellationToken ct)
    {
        var snapshot = await probe.GetBackupFreshnessAsync(ct).ConfigureAwait(false);
        return TypedResults.Ok(ToResponse(snapshot));
    }

    // Вкладка «Обслуживание» (MLC-217): планы обслуживания SQL (live-read sysmaintplan_* +
    // история заданий SQL Agent). ВСЕГДА Ok: проба never-throws, деградация (нет прав / SQL
    // Agent недоступен на Express / SQL недоступен) отражена статусом снимка
    // (PermissionDenied/AgentUnavailable/Unavailable), эндпоинт не 500-ит.
    internal static async Task<Ok<MaintenancePlansResponse>> GetMaintenancePlansAsync(
        [FromServices] IMaintenanceProbe probe,
        CancellationToken ct)
    {
        var snapshot = await probe.GetMaintenancePlansAsync(ct).ConfigureAwait(false);
        return TypedResults.Ok(ToResponse(snapshot));
    }

    // ── Мутации (только сервер 1С) ──────────────────────────────────────────────────────

    internal static Task<Results<Ok<ServerOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> StartOneCServerAsync(
        [FromBody] OneCServerStartRequest request,
        [FromServices] IServerStatusProvider provider,
        [FromServices] IWindowsServiceController controller,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteAsync(
            request.ServiceName, confirm: true, // запуск не разрушителен — Confirm не требуется
            controller.StartAsync,
            AuditActionType.OneCServerStarted, AuditDescriptions.OneCServerStarted,
            provider, audit, httpContext, loggerFactory, ct);

    internal static Task<Results<Ok<ServerOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> StopOneCServerAsync(
        [FromBody] OneCServerStopRequest request,
        [FromServices] IServerStatusProvider provider,
        [FromServices] IWindowsServiceController controller,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteAsync(
            request.ServiceName, request.Confirm,
            controller.StopAsync,
            AuditActionType.OneCServerStopped, AuditDescriptions.OneCServerStopped,
            provider, audit, httpContext, loggerFactory, ct);

    internal static Task<Results<Ok<ServerOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> RestartOneCServerAsync(
        [FromBody] OneCServerStopRequest request,
        [FromServices] IServerStatusProvider provider,
        [FromServices] IWindowsServiceController controller,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
        => ExecuteAsync(
            request.ServiceName, request.Confirm,
            controller.RestartAsync,
            AuditActionType.OneCServerRestarted, AuditDescriptions.OneCServerRestarted,
            provider, audit, httpContext, loggerFactory, ct);

    // Общий раннер мутации сервера 1С: Confirm-гейт → trim+валидация имени → whitelist по
    // discovery (нельзя дёргать произвольную службу / SQL / саму панель — требование
    // безопасности) → операция через IWindowsServiceController → маппинг
    // WindowsServiceOperationException в 409 → аудит при успехе (tenantId=null, server-scope).
    private static async Task<Results<Ok<ServerOperationResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> ExecuteAsync(
        string? rawName,
        bool confirm,
        Func<string, CancellationToken, Task<WindowsServiceOperationResult>> operation,
        AuditActionType action,
        Func<string, string, string> describe,
        IServerStatusProvider provider,
        IAuditLogger audit,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        // Серверный Confirm-гейт (защита от случайного клика помимо токена в UI).
        if (!confirm)
        {
            return TypedResults.Conflict(Problems.ServerConfirmRequired());
        }

        var name = (rawName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["ServiceName"] = ["Не указано имя службы сервера 1С."],
            });
        }

        // Whitelist цели: имя должно быть среди обнаруженных служб ragent. Нет совпадения →
        // 404 (нельзя управлять произвольной службой / SQL / самой панелью). Сравнение имён
        // служб Windows регистронезависимо (как ServiceController).
        var snapshot = await provider.GetStatusAsync(ct).ConfigureAwait(false);
        var match = snapshot.OneCServers
            .FirstOrDefault(s => string.Equals(s.ServiceName, name, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return TypedResults.NotFound();
        }

        // Точное имя берём из discovery (а не из запроса) — для корректной команды sc и аудита.
        var serviceName = match.ServiceName;
        var correlationId = httpContext.TraceIdentifier;
        WindowsServiceOperationResult result;
        try
        {
            result = await operation(serviceName, ct).ConfigureAwait(false);
        }
        catch (WindowsServiceOperationException ex)
        {
            LogServerOpFailed(loggerFactory.CreateLogger(typeof(ServerEndpoints).FullName!), serviceName, correlationId, ex);
            return TypedResults.Conflict(Problems.ServerOperationFailed(ex.Message, correlationId));
        }

        await httpContext.AuditAsync(audit, action, init => describe(serviceName, init), tenantId: null, ct).ConfigureAwait(false);
        return TypedResults.Ok(new ServerOperationResponse(result.ServiceName, result.FinalStatus.ToString()));
    }

    // ── Маппинг snapshot → DTO ──────────────────────────────────────────────────────────

    private static ServerStatusResponse ToResponse(ServerStatusSnapshot s) =>
        new(
            OneCServers: s.OneCServers
                .Select(o => new OneCServerDto(o.ServiceName, o.Running, o.PlatformVersion))
                .ToList(),
            Ras: new RasStatusDto(s.Ras.State, s.Ras.Running, s.Ras.ServiceName, s.Ras.Available, s.Ras.Error),
            Sql: new SqlStatusDto(s.Sql.Instance, s.Sql.ServiceName, s.Sql.Running, s.Sql.Available, s.Sql.Error),
            Iis: new IisStatusDto(s.Iis.State, s.Iis.Available, s.Iis.Error),
            Overall: s.Overall.ToString());

    private static BackupFreshnessResponse ToResponse(BackupFreshnessSnapshot s) =>
        new(
            Status: s.Status.ToString(),
            Databases: s.Databases
                .Select(d => new DatabaseBackupFreshnessDto(
                    d.DatabaseName, d.LastFullUtc, d.LastDiffUtc, d.LastLogUtc, d.IsStale))
                .ToList());

    private static MaintenancePlansResponse ToResponse(MaintenancePlansSnapshot s) =>
        new(
            Status: s.Status.ToString(),
            Plans: s.Plans
                .Select(p => new MaintenancePlanDto(
                    p.Name,
                    p.Subplans
                        .Select(sp => new MaintenanceSubplanDto(
                            sp.Name,
                            sp.HasSchedule,
                            sp.Outcome.ToString(),
                            sp.LastRunUtc,
                            sp.DurationSeconds,
                            sp.Tasks
                                .Select(t => new MaintenanceTaskDetailDto(t.Detail, t.Succeeded))
                                .ToList()))
                        .ToList()))
                .ToList());
}

public static partial class ServerEndpoints
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Сервер 1С: операция над «{ServiceName}» не удалась (correlationId={CorrelationId}).")]
    private static partial void LogServerOpFailed(ILogger logger, string serviceName, string correlationId, Exception ex);
}
