using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.TechLog;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

// Раздел «Расследование производительности» (MLC-239, трек 1.2, этап C). Зеркаль PerformanceEndpoints
// (раздел «Запись»): чтение = Viewer, мутации (старт/стоп/удаление) = Admin (ADR-26 дисциплина);
// старт/стоп идут через ITechLogCollectionService (он же аудирует 806/807), список/деталь/отчёт/прогресс
// читают AppDbContext напрямую (vertical slice ADR-20). Старт резолвит инфобазу из реестра
// (db.Infobases+Tenants, как PerformanceEndpoints.GetSqlAsync) — закрывает разрыв MLC-238: дело несёт
// InfobaseId/TenantId, изоляция арендатора — фильтр p:processName. Enum'ы на проводе — строкой
// (JsonStringEnumConverter, Program.cs). DELETE аудируется 809 (зеркаль PerfRecordingDeleted=702).
public static class InvestigationEndpoints
{
    public static void MapInvestigationEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/investigations")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Investigations");

        group.MapGet("", ListInvestigationsAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/{id:guid}", GetInvestigationAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/{id:guid}/report", GetReportAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/{id:guid}/progress", GetProgressAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("", StartInvestigationAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/stop", StopInvestigationAsync).RequireAuthorization(Roles.Admin);
        group.MapDelete("/{id:guid}", DeleteInvestigationAsync).RequireAuthorization(Roles.Admin);
    }

    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    // Десериализация ResultJson обратно в JsonElement (passthrough на провод как объект). Web-defaults
    // совпадают с тем, чем сериализовал конвейер (FindingJsonOptions = JsonSerializerDefaults.Web).
    private static readonly JsonSerializerOptions FindingReadOptions = new(JsonSerializerDefaults.Web);

    // ── Список дел (Viewer), свежие сверху, серверная пагинация (конверт). ──────────────────────────
    internal static async Task<Ok<InvestigationsPagedResponse>> ListInvestigationsAsync(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var p = page is > 0 ? page.Value : 1;
        var ps = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        var query = db.Investigations.AsNoTracking();
        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(i => i.StartedAtUtc)
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(i => new InvestigationSummary(
                i.Id, i.Scenario, i.Status, i.StartedAtUtc, i.StoppedAtUtc, i.StartedBy,
                i.StopReason, i.TenantId, i.InfobaseId, i.Findings.Count))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return TypedResults.Ok(new InvestigationsPagedResponse(items, total, p, ps));
    }

    // ── Деталь дела (Viewer) = шапка + снимок сбора + находки с разобранным result-объектом. 404, если нет. ──
    internal static async Task<Results<Ok<InvestigationDetail>, NotFound>> GetInvestigationAsync(
        Guid id,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var investigation = await db.Investigations
            .AsNoTracking()
            .Include(i => i.Findings)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            .ConfigureAwait(false);

        if (investigation is null)
        {
            return TypedResults.NotFound();
        }

        var summary = ToSummary(investigation, investigation.Findings.Count);
        var config = investigation.CollectionConfig is { } cfg ? ToConfigDto(cfg) : null;
        var findings = investigation.Findings.Select(ToFindingDto).ToList();

        return TypedResults.Ok(new InvestigationDetail(summary, config, findings));
    }

    // ── Отчёт (Viewer): ранжированные находки + текстовые рекомендации (шаблоны). ───────────────────
    // Ограничение этапа C: глубокого движка рекомендаций нет (best-effort, расширяется в D). Ранг —
    // эвристика по числу записей в результате анализатора; рекомендации — статические шаблоны по Kind.
    internal static async Task<Results<Ok<InvestigationReport>, NotFound>> GetReportAsync(
        Guid id,
        [FromServices] AppDbContext db,
        [FromServices] TimeProvider clock,
        CancellationToken ct)
    {
        var investigation = await db.Investigations
            .AsNoTracking()
            .Include(i => i.Findings)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            .ConfigureAwait(false);

        if (investigation is null)
        {
            return TypedResults.NotFound();
        }

        var summary = ToSummary(investigation, investigation.Findings.Count);
        var items = investigation.Findings
            .Select(BuildReportItem)
            .OrderByDescending(r => r.Severity)
            .ThenByDescending(r => r.Count)
            .ToList();

        var report = new InvestigationReport(summary, clock.GetUtcNow().UtcDateTime, items);
        return TypedResults.Ok(report);
    }

    // ── Прогресс (Viewer): лёгкий поллинг. Status + старт + прошедшее время + (опц.) размер собранного. ──
    internal static async Task<Results<Ok<InvestigationProgress>, NotFound>> GetProgressAsync(
        Guid id,
        [FromServices] AppDbContext db,
        [FromServices] ILogcfgStore store,
        [FromServices] TimeProvider clock,
        CancellationToken ct)
    {
        var investigation = await db.Investigations
            .AsNoTracking()
            .Select(i => new { i.Id, i.Status, i.StartedAtUtc, i.StoppedAtUtc, i.CollectionDirectory })
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            .ConfigureAwait(false);

        if (investigation is null)
        {
            return TypedResults.NotFound();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        // Прошедшее время: для завершённого дела — окно сбора; для активного — от старта до «сейчас».
        var end = investigation.StoppedAtUtc ?? now;
        var elapsed = Math.Max(0, (end - investigation.StartedAtUtc).TotalSeconds);

        // Размер собранного читаем за seam'ом store ТОЛЬКО для активного дела (каталог ещё на месте;
        // снятие удаляет сырьё после анализа). Дёшево, без тяжёлых JOIN.
        long? collectedBytes = investigation.Status == InvestigationStatus.Collecting
                               && !string.IsNullOrWhiteSpace(investigation.CollectionDirectory)
            ? store.GetDirectorySizeBytes(investigation.CollectionDirectory)
            : null;

        return TypedResults.Ok(new InvestigationProgress(
            investigation.Id, investigation.Status, investigation.StartedAtUtc, elapsed, collectedBytes));
    }

    // ── Старт расследования (Admin). Резолвит инфобазу из реестра, привязывает дело к арендатору. ──
    internal static async Task<Results<Created<InvestigationSummary>, Conflict<ProblemDetails>, NotFound>> StartInvestigationAsync(
        StartInvestigationRequest request,
        [FromServices] ITechLogCollectionService service,
        [FromServices] AppDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Резолв инфобазы (как PerformanceEndpoints.GetSqlAsync сшивает базу→клиент): из InfobaseId
        // берём Name (→ p:processName) и TenantId. Несуществующий InfobaseId → 404 (нечего собирать).
        Guid? tenantId = null;
        string? infobaseProcessName = null;
        if (request.InfobaseId is { } infobaseId)
        {
            var ib = await db.Infobases
                .AsNoTracking()
                .Where(x => x.Id == infobaseId)
                .Select(x => new { x.TenantId, x.Name })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (ib is null)
            {
                return TypedResults.NotFound();
            }

            tenantId = ib.TenantId;
            infobaseProcessName = ib.Name;
        }

        var initiator = httpContext.ResolveInitiator();
        var scenario = (TechLogScenario)(int)request.Scenario;
        var result = await service
            .InstallAsync(initiator, scenario, infobaseProcessName, ct, request.InfobaseId, tenantId)
            .ConfigureAwait(false);

        if (result.Outcome != TechLogStartOutcome.Started)
        {
            // Уже идёт — отдельный код с тем же текстом, что и удаление активного (409 InvestigationActive).
            // Прочие исходы (нет прав/места/корень/агент) — InvestigationStartFailed с причиной (+ icacls).
            if (result.Outcome == TechLogStartOutcome.AlreadyActive)
            {
                return TypedResults.Conflict(Problems.InvestigationActive());
            }

            var detail = ComposeStartFailureDetail(result);
            return TypedResults.Conflict(Problems.InvestigationStartFailed(detail));
        }

        // Сервис аудирует старт (806) и персистит дело в своём scope; читаем шапку обратно.
        var summary = await LoadSummaryAsync(db, result.CollectionId, ct).ConfigureAwait(false);
        return summary is null
            ? TypedResults.NotFound()
            : TypedResults.Created($"/api/v1/investigations/{summary.Id}", summary);
    }

    // ── Ручной стоп активного дела (Admin). NotActive → 409 (id не текущей активной). ───────────────
    internal static async Task<Results<Ok<InvestigationSummary>, Conflict<ProblemDetails>, NotFound>> StopInvestigationAsync(
        Guid id,
        [FromServices] ITechLogCollectionService service,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var outcome = await service
            .RemoveAsync(id, InvestigationStopReason.Manual, ct)
            .ConfigureAwait(false);

        if (outcome == TechLogStopOutcome.NotActive)
        {
            return TypedResults.Conflict(Problems.InvestigationNotActive());
        }

        // Снятие сервис аудирует (807). Дело после конвейера — Analyzing/Completed/Failed; читаем шапку.
        var summary = await LoadSummaryAsync(db, id, ct).ConfigureAwait(false);
        return summary is null ? TypedResults.NotFound() : TypedResults.Ok(summary);
    }

    // ── Удаление дела (Admin) — каскадом сносит Finding'и (FK Cascade). Активное дело удалить нельзя:
    //    409 INVESTIGATION_ACTIVE (сначала остановить, иначе конвейер пишет в удалённое). 404, если нет.
    internal static async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>>> DeleteInvestigationAsync(
        Guid id,
        [FromServices] AppDbContext db,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var investigation = await db.Investigations
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            .ConfigureAwait(false);

        if (investigation is null)
        {
            return TypedResults.NotFound();
        }

        // Активным считаем дело в активных фазах конвейера (сбор/разбор): сбор пишет в каталог, разбор
        // наполняет Finding'и — удаление под ними оставит сирот. Завершённые/прерванные/упавшие удалимы.
        if (investigation.Status is InvestigationStatus.Collecting or InvestigationStatus.Analyzing)
        {
            return TypedResults.Conflict(Problems.InvestigationActive());
        }

        db.Investigations.Remove(investigation);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Аудит после успешного удаления (host/server-scope, tenantId=null — сбор охватывает узел).
        await httpContext.AuditAsync(audit, AuditActionType.InvestigationDeleted,
            init => AuditDescriptions.InvestigationDeleted(investigation.Id, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    // ── Маппинг и хелперы ──────────────────────────────────────────────────────────────────────────

    private static async Task<InvestigationSummary?> LoadSummaryAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        await db.Investigations
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new InvestigationSummary(
                i.Id, i.Scenario, i.Status, i.StartedAtUtc, i.StoppedAtUtc, i.StartedBy,
                i.StopReason, i.TenantId, i.InfobaseId, i.Findings.Count))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

    private static InvestigationSummary ToSummary(Investigation i, int findingsCount) => new(
        i.Id, i.Scenario, i.Status, i.StartedAtUtc, i.StoppedAtUtc, i.StartedBy,
        i.StopReason, i.TenantId, i.InfobaseId, findingsCount);

    private static CollectionConfigDto ToConfigDto(CollectionConfig c) => new(
        c.LogcfgLocation, c.Events, c.DurationThresholdMicros, c.ProcessNameFilter, c.Format, c.HistoryHours);

    // Десериализация ResultJson → JsonElement (passthrough на провод как объект анализатора).
    // Пустой/битый JSON → пустой объект (никогда не валим деталь из-за одной находки).
    private static FindingDto ToFindingDto(Finding f)
    {
        JsonElement result;
        try
        {
            result = string.IsNullOrWhiteSpace(f.ResultJson)
                ? EmptyObject()
                : JsonSerializer.Deserialize<JsonElement>(f.ResultJson, FindingReadOptions);
        }
        catch (JsonException)
        {
            result = EmptyObject();
        }

        return new FindingDto(f.Kind, f.SchemaVersion, result);
    }

    private static JsonElement EmptyObject() => JsonDocument.Parse("{}").RootElement.Clone();

    // Текст причины провала старта (детал ProblemDetails). Где сервис даёт точную команду icacls
    // (NoWriteAccess/AgentNoCollectionAccess) — дописываем её, как RAS-healing/диск-гард.
    private static string ComposeStartFailureDetail(TechLogStartResult result)
    {
        var issue = string.IsNullOrWhiteSpace(result.Issue)
            ? result.Outcome switch
            {
                TechLogStartOutcome.RootNotFound =>
                    "Не найден каталог conf платформы 1С. Проверьте установку 1С на узле.",
                TechLogStartOutcome.NoWriteAccess =>
                    "Недостаточно прав на запись logcfg.xml в каталог conf платформы 1С.",
                TechLogStartOutcome.InsufficientDiskSpace =>
                    "Недостаточно свободного места для сбора технологического журнала.",
                TechLogStartOutcome.AgentNoCollectionAccess =>
                    "У аккаунта агента 1С нет прав записи на каталог сбора технологического журнала.",
                _ => "Не удалось запустить сбор технологического журнала.",
            }
            : result.Issue;

        return string.IsNullOrWhiteSpace(result.GrantCommand)
            ? issue
            : $"{issue} Команда выдачи прав: {result.GrantCommand}";
    }

    // Ранжирование находки в отчёт (best-effort, этап C): severity по числу записей в результате,
    // рекомендация — шаблон по Kind. Глубокого движка нет (расширяется в D). Count считаем по
    // ключевому массиву результата анализатора (разобрав ResultJson).
    private static ReportItem BuildReportItem(Finding f)
    {
        var count = CountFindings(f);
        var severity = count switch
        {
            0 => ReportSeverity.None,
            <= 5 => ReportSeverity.Info,
            _ => ReportSeverity.Warning,
        };

        var (headline, recommendation) = f.Kind switch
        {
            FindingKind.ManagedLocks => (
                "Управляемые блокировки 1С",
                "Обнаружены ожидания/таймауты управляемых блокировок. Разберите цепочки ожидания и контекст "
                + "транзакций: длинные транзакции и пересекающиеся блокировки на одних объектах — частая причина."),
            FindingKind.SlowQueries => (
                "Долгие запросы к СУБД",
                "Обнаружены долгие запросы к СУБД. Сгруппируйте похожие по тексту, проверьте индексы и планы; "
                + "повторяющийся тяжёлый запрос — кандидат на оптимизацию."),
            FindingKind.Exceptions => (
                "Исключения платформы 1С",
                "Обнаружены исключения платформы. Разберите частые типы и тексты: повторяющиеся ошибки указывают "
                + "на дефект конфигурации или проблему окружения."),
            FindingKind.DbmsLocks => (
                "Блокировки уровня СУБД",
                "Обнаружены блокировки уровня СУБД. Сопоставьте жертв и источники по дереву ожидания; "
                + "эскалация блокировок и долгие транзакции на горячих таблицах — типичная причина."),
            _ => ("Находка анализа", "Разберите детали находки в карточке дела."),
        };

        if (count == 0)
        {
            recommendation = "Анализатор отработал, значимых находок этого вида не обнаружено.";
        }

        return new ReportItem(f.Kind, severity, count, headline, recommendation);
    }

    // Эвристический счётчик находок по ключевому массиву результата анализатора (разбор ResultJson).
    // Не привязываемся к точной форме DTO — берём наибольший массив верхнего уровня (TopQueries/
    // WaitEdges/Timeouts/Deadlocks/TopExceptions). Битый/пустой JSON → 0. Best-effort (этап C).
    private static int CountFindings(Finding f)
    {
        if (string.IsNullOrWhiteSpace(f.ResultJson))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(f.ResultJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            var max = 0;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    max = Math.Max(max, prop.Value.GetArrayLength());
                }
            }

            return max;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
