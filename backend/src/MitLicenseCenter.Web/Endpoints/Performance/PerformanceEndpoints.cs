using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;

namespace MitLicenseCenter.Web.Endpoints;

// Раздел «Быстродействие» (MLC-064/066/068, ADR-26). Live-снимки: pull-по-требованию,
// ничего не персистится (фронт поллит ~5с, пока вкладка открыта). Vertical slice (ADR-20)
// — эндпоинты зовут Application-порты (IHostMetricsProbe / IClusterClient / ISqlPerformanceProbe),
// к WMI/Process/rac.exe/DMV напрямую не ходят (но свой AppDbContext читать можно — атрибуция SQL
// по клиенту). Чтение = Viewer (управление записью прибудет в Фазе 4 как Admin).
public static class PerformanceEndpoints
{
    public static void MapPerformanceEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/performance")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Performance");

        group.MapGet("/host", GetHostAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/onec-sessions", GetOneCSessionsAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/sql", GetSqlAsync).RequireAuthorization(Roles.Viewer);

        // Recording (MLC-070, Фаза 4): чтение списка/просмотр = Viewer; управление (старт/стоп/удаление)
        // = Admin (ADR-26: live-чтение Viewer, управление записью Admin).
        group.MapGet("/recordings", ListRecordingsAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/recordings/{id:guid}", GetRecordingAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("/recordings", StartRecordingAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/recordings/{id:guid}/stop", StopRecordingAsync).RequireAuthorization(Roles.Admin);
        group.MapDelete("/recordings/{id:guid}", DeleteRecordingAsync).RequireAuthorization(Roles.Admin);
    }

    // Live-снимок метрик хоста «сейчас». Measuring=true на первом poll'е (CPU% процессов и
    // латентность диска требуют дельты двух замеров). Адаптер защитный — недоступная метрика
    // деградирует в 0, снимок отдаётся всегда.
    internal static async Task<Ok<HostMetricsSnapshot>> GetHostAsync(
        [FromServices] IHostMetricsProbe probe,
        CancellationToken ct)
    {
        var snapshot = await probe.CaptureAsync(ct).ConfigureAwait(false);
        return TypedResults.Ok(snapshot);
    }

    // Live-снимок нагрузки 1С «кто грузит» (MLC-066): сеансы с perf-полями (`rac session list`)
    // + рабочие процессы (`rac process list`) — 2 спавна rac.exe на poll (spawn-бюджет ADR-3.3).
    // Пустые списки = rac не настроен/недоступен (best-effort). Ничего не персистится (ADR-26).
    internal static async Task<Ok<OneCLoadSnapshot>> GetOneCSessionsAsync(
        [FromServices] IClusterClient cluster,
        CancellationToken ct)
    {
        var sessions = await cluster.ListSessionLoadsAsync(ct).ConfigureAwait(false);
        var processes = await cluster.ListProcessesAsync(ct).ConfigureAwait(false);
        return TypedResults.Ok(new OneCLoadSnapshot(DateTime.UtcNow, sessions, processes));
    }

    // Live-снимок нагрузки на MSSQL «1С грузит SQL?» (MLC-068, Фаза 3): активные запросы +
    // цепочки блокировок + IO-stall + дельта wait-stats через порт ISqlPerformanceProbe (только
    // он ходит в DMV — ADR-20). Эндпоинт добавляет атрибуцию по клиенту: сшивает имена баз из
    // DMV с инфобазами панели (database→Infobase→tenant) по своему AppDbContext — это разрешённый
    // прямой доступ vertical slice к собственной БД, не к DMV. Нет права VIEW SERVER STATE →
    // снимок со Status=PermissionDenied (фронт рисует degraded-баннер), не пустой «всё спокойно».
    internal static async Task<Ok<SqlPerformanceView>> GetSqlAsync(
        [FromServices] ISqlPerformanceProbe probe,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var snapshot = await probe.CaptureAsync(ct).ConfigureAwait(false);

        var refs = await db.Infobases
            .Join(
                db.Tenants,
                ib => ib.TenantId,
                t => t.Id,
                (ib, t) => new InfobaseDatabaseRef(ib.DatabaseName, ib.TenantId, t.Name, ib.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var attribution = SqlAttributionResolver.Resolve(snapshot.DatabaseNames(), refs);
        return TypedResults.Ok(new SqlPerformanceView(snapshot, attribution));
    }

    // ── Recording (MLC-070, Фаза 4) ──────────────────────────────────────────────────────────

    // Список расследований (Viewer), свежие сверху. SampleCount — число собранных сэмплов.
    internal static async Task<Ok<IReadOnlyList<RecordingSummary>>> ListRecordingsAsync(
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var items = await db.PerfRecordings
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAtUtc)
            .Select(r => new RecordingSummary(
                r.Id, r.StartedAtUtc, r.StoppedAtUtc, r.Status, r.StartedBy, r.StopReason, r.Samples.Count))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<RecordingSummary>>(items);
    }

    // Просмотр записи (Viewer) = метаданные + ряд сэмплов по времени. JSON-колонки сэмпла
    // (семьи / топ-виновники 1С/SQL) десериализуются здесь через PerfSampleJson — обратно в те же
    // Application-записи, что отдаёт live-снимок. 404, если записи нет.
    internal static async Task<Results<Ok<RecordingDetail>, NotFound>> GetRecordingAsync(
        Guid id,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var recording = await db.PerfRecordings
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            .ConfigureAwait(false);

        if (recording is null)
        {
            return TypedResults.NotFound();
        }

        var samples = await db.PerfRecordingSamples
            .AsNoTracking()
            .Where(s => s.RecordingId == id)
            .OrderBy(s => s.SampleUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var summary = new RecordingSummary(
            recording.Id, recording.StartedAtUtc, recording.StoppedAtUtc,
            recording.Status, recording.StartedBy, recording.StopReason, samples.Count);

        return TypedResults.Ok(new RecordingDetail(summary, samples.Select(MapSample).ToList()));
    }

    // Старт записи (Admin). Одна запись за раз: уже идёт → 409 RECORDING_ACTIVE с id текущей.
    internal static async Task<Results<Created<RecordingSummary>, Conflict<ProblemDetails>, NotFound>> StartRecordingAsync(
        [FromServices] IPerfRecordingService recordingService,
        [FromServices] AppDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var initiator = httpContext.User.Identity?.Name ?? "Unknown";
        var result = await recordingService.StartAsync(initiator, ct).ConfigureAwait(false);

        if (result.Outcome == PerfRecordingStartOutcome.AlreadyActive)
        {
            return TypedResults.Conflict(Problems.RecordingActive());
        }

        var summary = await LoadSummaryAsync(db, result.RecordingId, ct).ConfigureAwait(false);
        return summary is null
            ? TypedResults.NotFound()
            : TypedResults.Created($"/api/v1/performance/recordings/{summary.Id}", summary);
    }

    // Ручной стоп активной записи (Admin). NotActive (id не текущей активной) → 404.
    internal static async Task<Results<Ok<RecordingSummary>, NotFound>> StopRecordingAsync(
        Guid id,
        [FromServices] IPerfRecordingService recordingService,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var outcome = await recordingService.StopAsync(id, ct).ConfigureAwait(false);
        if (outcome == PerfRecordingStopOutcome.NotActive)
        {
            return TypedResults.NotFound();
        }

        var summary = await LoadSummaryAsync(db, id, ct).ConfigureAwait(false);
        return summary is null ? TypedResults.NotFound() : TypedResults.Ok(summary);
    }

    // Удаление записи (Admin) — каскадом сносит сэмплы (FK Cascade). Идущую запись удалить нельзя:
    // 409 RECORDING_ACTIVE (сначала остановить, иначе фоновый сэмплер продолжит писать). 404, если нет.
    internal static async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>>> DeleteRecordingAsync(
        Guid id,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var recording = await db.PerfRecordings
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            .ConfigureAwait(false);

        if (recording is null)
        {
            return TypedResults.NotFound();
        }

        if (recording.Status == PerfRecordingStatus.Active)
        {
            return TypedResults.Conflict(Problems.RecordingActive());
        }

        db.PerfRecordings.Remove(recording);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<RecordingSummary?> LoadSummaryAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        await db.PerfRecordings
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new RecordingSummary(
                r.Id, r.StartedAtUtc, r.StoppedAtUtc, r.Status, r.StartedBy, r.StopReason, r.Samples.Count))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

    private static RecordingSampleDto MapSample(PerfRecordingSample s) => new(
        s.SampleUtc,
        s.Measuring,
        s.CpuPercent,
        s.CpuQueueLength,
        s.MemoryAvailableMBytes,
        s.MemoryTotalMBytes,
        s.MemoryPagesPerSec,
        s.DiskAvgReadSecPerOp,
        s.DiskAvgWriteSecPerOp,
        s.DiskQueueLength,
        s.ProcessesInaccessible,
        JsonSerializer.Deserialize<List<ProcessGroupUsage>>(s.ProcessGroupsJson, PerfSampleJson.Options) ?? [],
        s.OneCLoadJson is null ? null : JsonSerializer.Deserialize<OneCLoadSnapshot>(s.OneCLoadJson, PerfSampleJson.Options),
        s.SqlLoadJson is null ? null : JsonSerializer.Deserialize<SqlPerformanceSnapshot>(s.SqlLoadJson, PerfSampleJson.Options));
}
