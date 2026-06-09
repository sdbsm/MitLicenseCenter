using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;

namespace MitLicenseCenter.Web.Endpoints;

// On-demand бэкап баз SQL (MLC-077, ADR-27). Vertical slice (ADR-20): список/просмотр/
// удаление читают свой AppDbContext напрямую; постановка в очередь и server-side файловые
// операции — только через Application-порты (IBackupOrchestrator / ISqlBackupService),
// к BACKUP/xp_* эндпоинты не ходят. Роли: запуск = Viewer (операторы), удаление = Admin
// (ADR-27); фронт поллит список, пока есть Queued/Running.
public static class BackupsEndpoints
{
    public static void MapBackupsEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/backups")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Backups");

        group.MapGet("/", ListAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("/", StartAsync).RequireAuthorization(Roles.Viewer);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(Roles.Admin);
    }

    // Список бэкапов (Viewer), свежие сверху; ?infobaseId= — бэкапы одной инфобазы
    // (диалог на её карточке).
    internal static async Task<Ok<IReadOnlyList<BackupSummary>>> ListAsync(
        [FromQuery] Guid? infobaseId,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var query = db.DatabaseBackups.AsNoTracking();
        if (infobaseId is { } infobase)
        {
            query = query.Where(b => b.InfobaseId == infobase);
        }

        var items = await query
            .OrderByDescending(b => b.RequestedAtUtc)
            .Select(b => new BackupSummary(
                b.Id, b.InfobaseId, b.DatabaseServer, b.DatabaseName, b.Status, b.RequestedBy,
                b.RequestedAtUtc, b.StartedAtUtc, b.CompletedAtUtc, b.FilePath, b.FileSizeBytes,
                b.FailureReason, b.ErrorMessage))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<BackupSummary>>(items);
    }

    internal static async Task<Results<Ok<BackupSummary>, NotFound>> GetAsync(
        Guid id,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var summary = await LoadSummaryAsync(db, id, ct).ConfigureAwait(false);
        return summary is null ? TypedResults.NotFound() : TypedResults.Ok(summary);
    }

    // Постановка бэкапа в очередь (Viewer — операторская кнопка, ADR-27). Пара server+db
    // снимается с инфобазы; дубль активной базы → 409 BACKUP_ACTIVE; незаданная папка →
    // честный 409 BACKUP_FOLDER_NOT_CONFIGURED ещё до постановки.
    internal static async Task<Results<Created<BackupSummary>, NotFound, Conflict<ProblemDetails>>> StartAsync(
        StartBackupRequest request,
        [FromServices] IBackupOrchestrator orchestrator,
        [FromServices] ISettingsSnapshot settings,
        [FromServices] AppDbContext db,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var infobase = await db.Infobases
            .AsNoTracking()
            .FirstOrDefaultAsync(ib => ib.Id == request.InfobaseId, ct)
            .ConfigureAwait(false);

        if (infobase is null)
        {
            return TypedResults.NotFound();
        }

        if (string.IsNullOrWhiteSpace(settings.GetString(SettingKey.BackupFolderPath)))
        {
            return TypedResults.Conflict(Problems.BackupFolderNotConfigured());
        }

        var result = await orchestrator.RequestAsync(
            infobase.Id, infobase.DatabaseServer, infobase.DatabaseName,
            httpContext.ResolveInitiator(), ct).ConfigureAwait(false);

        if (result.Outcome == BackupRequestOutcome.AlreadyActive)
        {
            return TypedResults.Conflict(Problems.BackupActive());
        }

        // Единственная запись аудита со связкой «бэкап ↔ клиент»: у DatabaseBackup нет FK,
        // итоговые 511/512 пишет оркестратор без tenantId.
        await httpContext.AuditAsync(audit, AuditActionType.BackupRequested,
            init => AuditDescriptions.BackupRequested(infobase.DatabaseName, init),
            tenantId: infobase.TenantId, ct).ConfigureAwait(false);

        var summary = await LoadSummaryAsync(db, result.BackupId, ct).ConfigureAwait(false);
        return summary is null
            ? TypedResults.NotFound()
            : TypedResults.Created($"/api/v1/backups/{summary.Id}", summary);
    }

    // Удаление бэкапа (Admin): сначала server-side файл (keep-latest-1 означает «в подпапке
    // ровно этот файл» → cutoff=сейчас сносит его xp_delete_file), затем строка. Провал
    // удаления файла → строку НЕ трогаем (иначе осиротевший .bak станет невидимым для
    // ручного удаления; ночной TTL его всё равно подчистит). Running → 409 (образец
    // DeleteRecordingAsync: незавершённую операцию не удаляем).
    internal static async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>>> DeleteAsync(
        Guid id,
        [FromServices] AppDbContext db,
        [FromServices] ISqlBackupService backupService,
        [FromServices] IAuditLogger audit,
        [FromServices] TimeProvider clock,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var backup = await db.DatabaseBackups
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            .ConfigureAwait(false);

        if (backup is null)
        {
            return TypedResults.NotFound();
        }

        if (backup.Status == BackupStatus.Running)
        {
            return TypedResults.Conflict(Problems.BackupActive());
        }

        if (!string.IsNullOrEmpty(backup.FilePath) &&
            Path.GetDirectoryName(backup.FilePath) is { Length: > 0 } folder)
        {
            var result = await backupService
                .DeleteBackupsOlderThanAsync(backup.DatabaseServer, folder, clock.GetUtcNow().UtcDateTime, ct)
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                // Технические детали адаптер уже записал в журнал сервера (never-throws).
                return TypedResults.Conflict(Problems.BackupDeleteFailed());
            }
        }

        db.DatabaseBackups.Remove(backup);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.BackupDeleted,
            init => AuditDescriptions.BackupDeleted(backup.DatabaseName, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<BackupSummary?> LoadSummaryAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        await db.DatabaseBackups
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BackupSummary(
                b.Id, b.InfobaseId, b.DatabaseServer, b.DatabaseName, b.Status, b.RequestedBy,
                b.RequestedAtUtc, b.StartedAtUtc, b.CompletedAtUtc, b.FilePath, b.FileSizeBytes,
                b.FailureReason, b.ErrorMessage))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
}
