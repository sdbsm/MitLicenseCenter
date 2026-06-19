using System.Globalization;
using System.Text.RegularExpressions;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Maintenance;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Web.Hangfire;

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

        // Рабочие процессы 1С (rphost) — список во вкладке «Службы» (MLC-219). Viewer;
        // деградация пустым списком (rac недоступен/не настроен), эндпоинт не 500-ит.
        group.MapGet("/onec/processes", GetOneCProcessesAsync).RequireAuthorization(Roles.Viewer);

        // Рестарт рабочего процесса 1С (rphost) по Pid (MLC-220, ADR-56). Admin + Confirm-гейт:
        // завершение ОС-процесса rphost (whitelist по rac process list + guard по имени), кластер
        // авто-поднимает новый. Pid не в whitelist → 404; Pid переиспользован / не исчез за
        // таймаут → 409; не-confirm → 409 PROCESS_CONFIRM_REQUIRED.
        group.MapPost("/onec/processes/restart", RestartOneCProcessAsync).RequireAuthorization(Roles.Admin);

        group.MapPost("/onec/start", StartOneCServerAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/onec/stop", StopOneCServerAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/onec/restart", RestartOneCServerAsync).RequireAuthorization(Roles.Admin);

        // Расписание авто-рестартов сервера 1С (MLC-218): чтение — Viewer, изменение — Admin.
        group.MapGet("/auto-restart", GetAutoRestartScheduleAsync).RequireAuthorization(Roles.Viewer);
        group.MapPut("/auto-restart", SetAutoRestartScheduleAsync).RequireAuthorization(Roles.Admin);
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

    // Вкладка «Службы» (MLC-219): рабочие процессы 1С (`rphost`) — список через `rac process
    // list` (тот же live-pull, что и «Быстродействие»). ВСЕГДА Ok: адаптер never-throws на сбое
    // (rac не настроен/недоступен → пустой список), эндпоинт не 500-ит. Рестарт процесса НЕ
    // реализован (исследовательская часть, разведка не подтвердила безопасный механизм через
    // rac — у rac нет «restart process»; OS-kill по Pid требует проверки на живом кластере).
    internal static async Task<Ok<OneCProcessesResponse>> GetOneCProcessesAsync(
        [FromServices] IClusterClient cluster,
        CancellationToken ct)
    {
        var processes = await cluster.ListProcessesAsync(ct).ConfigureAwait(false);
        var dtos = processes
            .Select(p => new OneCProcessDto(p.Process, p.Pid, p.AvailablePerformance, p.AvgCallTime, p.MemorySize))
            .ToList();
        return TypedResults.Ok(new OneCProcessesResponse(dtos));
    }

    // Рестарт рабочего процесса 1С (rphost) по Pid (MLC-220, ADR-56). Admin + Confirm-гейт:
    // у rac нет «restart process», поэтому рестарт = завершение ОС-процесса rphost по Pid с
    // авто-подъёмом кластером. Сервис страхует операцию (whitelist по rac process list →
    // guard по имени процесса → kill → верификация исчезновения Pid с таймаутом). Маппинг
    // исхода: NotInCluster → 404; PidReused/VerificationTimedOut → 409; Restarted → 200 + аудит.
    internal static async Task<Results<Ok<OneCProcessRestartResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> RestartOneCProcessAsync(
        [FromBody] OneCProcessRestartRequest request,
        [FromServices] IOneCProcessRestartService restart,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Серверный Confirm-гейт (защита от случайного клика помимо токена в UI).
        if (!request.Confirm)
        {
            return TypedResults.Conflict(Problems.ProcessConfirmRequired());
        }

        // Pid должен быть положительным (ОС-идентификатор процесса). Невалидный → 400.
        if (request.Pid <= 0)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Pid"] = ["Ожидается положительный Pid рабочего процесса 1С."],
            });
        }

        var correlationId = httpContext.TraceIdentifier;
        var result = await restart.RestartAsync(request.Pid, ct).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case OneCProcessRestartOutcome.NotInCluster:
                // Pid не в текущем rac process list (whitelist не пройден) — нельзя
                // завершать произвольный ОС-процесс. Аудит не пишется.
                return TypedResults.NotFound();

            case OneCProcessRestartOutcome.PidReused:
                return TypedResults.Conflict(Problems.ProcessRestartFailed(
                    "Процесс с этим Pid больше не является рабочим процессом 1С (Pid переиспользован системой). "
                        + "Обновите список и повторите.",
                    correlationId));

            case OneCProcessRestartOutcome.VerificationTimedOut:
                return TypedResults.Conflict(Problems.ProcessRestartFailed(
                    "Рабочий процесс 1С не был заменён кластером за отведённое время. "
                        + "Проверьте состояние сервера 1С и повторите при необходимости.",
                    correlationId));

            case OneCProcessRestartOutcome.Restarted:
            default:
                await httpContext.AuditAsync(
                    audit,
                    AuditActionType.OneCProcessRestarted,
                    init => AuditDescriptions.OneCProcessRestarted(result.Pid, init),
                    tenantId: null,
                    ct).ConfigureAwait(false);
                return TypedResults.Ok(new OneCProcessRestartResponse(result.Pid, result.Outcome.ToString()));
        }
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

    // ── Расписание авто-рестартов (MLC-218) ─────────────────────────────────────────────

    private const string DefaultAutoRestartTime = "04:00";

    // GET /server/auto-restart (Viewer): текущее расписание + время последнего прогона +
    // целевые службы (запущенные ragent из снимка статуса). Never-throws на стороне статуса
    // (провайдер деградирует флагами); настройки читаются из store. Дефолты — если ключи ещё
    // не засеяны (enabled=false, time=04:00).
    internal static async Task<Ok<AutoRestartScheduleResponse>> GetAutoRestartScheduleAsync(
        [FromServices] ISettingsStore store,
        [FromServices] IServerStatusProvider provider,
        CancellationToken ct)
    {
        var enabled = await store.GetIntAsync(SettingKey.OneCAutoRestartEnabled, ct).ConfigureAwait(false) == 1;
        var time = await store.GetAsync(SettingKey.OneCAutoRestartTime, ct).ConfigureAwait(false);
        var lastRunRaw = await store.GetAsync(SettingKey.OneCAutoRestartLastRunUtc, ct).ConfigureAwait(false);

        var snapshot = await provider.GetStatusAsync(ct).ConfigureAwait(false);
        var targets = snapshot.OneCServers
            .Where(s => s.Running)
            .Select(s => s.ServiceName)
            .ToList();

        return TypedResults.Ok(new AutoRestartScheduleResponse(
            Enabled: enabled,
            Time: string.IsNullOrWhiteSpace(time) ? DefaultAutoRestartTime : time,
            LastRunUtc: ParseLastRun(lastRunRaw),
            TargetServices: targets));
    }

    // PUT /server/auto-restart (Admin): валидация HH:mm → запись enabled+time → перерегистрация
    // джобы (Apply: вкл → AddOrUpdate с дневным cron в местном поясе, выкл → RemoveIfExists) →
    // аудит изменения настройки (код 804). Невалидное время → ValidationProblem (мутация не идёт).
    internal static async Task<Results<Ok<AutoRestartScheduleResponse>, ValidationProblem>> SetAutoRestartScheduleAsync(
        [FromBody] AutoRestartScheduleRequest request,
        [FromServices] ISettingsStore store,
        [FromServices] IServerStatusProvider provider,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var time = (request.Time ?? string.Empty).Trim();
        if (!AutoRestartTimeRegex().IsMatch(time))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Time"] = ["Ожидается время в формате ЧЧ:мм (00:00–23:59)."],
            });
        }

        // Нормализуем к HH:mm с ведущими нулями (cron-билдер допускает "4:0", но хранить и
        // показывать удобнее канонично).
        var canonical = $"{int.Parse(time.Split(':')[0], CultureInfo.InvariantCulture):00}:{int.Parse(time.Split(':')[1], CultureInfo.InvariantCulture):00}";

        var initiator = httpContext.ResolveInitiator();
        await store.SetAsync(SettingKey.OneCAutoRestartEnabled, request.Enabled ? "1" : "0", isSecret: false, initiator, ct).ConfigureAwait(false);
        await store.SetAsync(SettingKey.OneCAutoRestartTime, canonical, isSecret: false, initiator, ct).ConfigureAwait(false);

        // Перерегистрация джобы по новой настройке (включено → дневной cron из времени в
        // местном поясе; выключено → снять задание). НЕ тик-каждые-5-минут.
        OneCAutoRestartScheduler.Apply(request.Enabled, canonical);

        await httpContext.AuditAsync(
            audit,
            AuditActionType.OneCServerAutoRestartScheduleChanged,
            init => AuditDescriptions.OneCServerAutoRestartScheduleChanged(request.Enabled, canonical, init),
            tenantId: null,
            ct).ConfigureAwait(false);

        // Возвращаем актуализированное состояние (как GET) — FE сразу показывает сохранённое.
        var lastRunRaw = await store.GetAsync(SettingKey.OneCAutoRestartLastRunUtc, ct).ConfigureAwait(false);
        var snapshot = await provider.GetStatusAsync(ct).ConfigureAwait(false);
        var targets = snapshot.OneCServers.Where(s => s.Running).Select(s => s.ServiceName).ToList();

        return TypedResults.Ok(new AutoRestartScheduleResponse(
            Enabled: request.Enabled,
            Time: canonical,
            LastRunUtc: ParseLastRun(lastRunRaw),
            TargetServices: targets));
    }

    // Парсинг отметки «прошлого прогона»: хранится инвариантным round-trip "O" (UTC). Мусор/
    // отсутствие → null (поле опускается на проводе). Возвращаем именно UTC DateTime.
    private static DateTime? ParseLastRun(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTime.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed.ToUniversalTime()
            : null;
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

    // MLC-218 — время авто-рестарта HH:mm (00:00–23:59). Час 00–23, минута 00–59; ведущий
    // ноль часа опционален (допускаем "4:00"), эндпоинт канонизирует к двум разрядам.
    [GeneratedRegex(@"^([01]?\d|2[0-3]):[0-5]\d$", RegexOptions.CultureInvariant)]
    private static partial Regex AutoRestartTimeRegex();
}
