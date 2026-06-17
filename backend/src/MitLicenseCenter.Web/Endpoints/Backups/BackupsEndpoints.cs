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
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private const int MaxSearchLength = 200;

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
    // (диалог на её карточке). Серверная пагинация (MLC-130, BE-17): page/pageSize.
    // Опциональный поиск по DatabaseName через plain Contains → EF транслирует в LIKE '%term%';
    // регистронезависимость — за collation БД (CA1862 подавляем с обоснованием).
    internal static async Task<Results<Ok<BackupsPagedResponse>, ValidationProblem>> ListAsync(
        [FromQuery] Guid? infobaseId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search,
        [FromServices] AppDbContext db,
        [FromServices] ISqlBackupService backupService,
        CancellationToken ct)
    {
        var searchTerm = search?.Trim();
        if (searchTerm is { Length: > MaxSearchLength })
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(search)] = [$"Не длиннее {MaxSearchLength} символов."],
                });
        }

        var p = page is > 0 ? page.Value : 1;
        var ps = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        var query = db.DatabaseBackups.AsNoTracking();
        if (infobaseId is { } infobase)
        {
            query = query.Where(b => b.InfobaseId == infobase);
        }
        if (!string.IsNullOrEmpty(searchTerm))
        {
            // Подстрочный поиск по имени базы обычным string.Contains →
            // EF Core SQL Server-провайдер транслирует в `LIKE '%term%'`.
            // Регистронезависимость обеспечивает CI-collation БД, а не код.
            // OrdinalIgnoreCase SQL Server-провайдером НЕ транслируется (CA1862).
#pragma warning disable CA1862 // EF-запрос: трансляция в SQL LIKE, регистр — за collation БД
            query = query.Where(b => b.DatabaseName.Contains(searchTerm));
#pragma warning restore CA1862
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(b => b.RequestedAtUtc)
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(b => new BackupSummary(
                b.Id, b.InfobaseId, b.DatabaseServer, b.DatabaseName, b.Status, b.RequestedBy,
                b.RequestedAtUtc, b.StartedAtUtc, b.CompletedAtUtc, b.FilePath, b.FileSizeBytes,
                b.FailureReason, b.ErrorMessage, FileAvailable: null))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        items = await ApplyFileAvailabilityAsync(backupService, items, ct).ConfigureAwait(false);
        return TypedResults.Ok(new BackupsPagedResponse(items, total, p, ps));
    }

    internal static async Task<Results<Ok<BackupSummary>, NotFound>> GetAsync(
        Guid id,
        [FromServices] AppDbContext db,
        [FromServices] ISqlBackupService backupService,
        CancellationToken ct)
    {
        var summary = await LoadSummaryAsync(db, id, ct).ConfigureAwait(false);
        if (summary is null)
        {
            return TypedResults.NotFound();
        }

        var withAvailability = await ApplyFileAvailabilityAsync(backupService, [summary], ct).ConfigureAwait(false);
        return TypedResults.Ok(withAvailability[0]);
    }

    // Постановка бэкапа в очередь (Viewer — операторская кнопка, ADR-27). Имя БД снимается
    // с инфобазы, SQL-инстанс — из настройки Sql.Server (single-host, MLC-088); дубль
    // активной базы → 409 BACKUP_ACTIVE; незаданная папка/сервер → честный 409 ещё до постановки.
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

        // Сервер БД — единый SQL-инстанс из настройки (MLC-088), не per-база.
        var sqlServer = settings.GetString(SettingKey.SqlServer);
        if (string.IsNullOrWhiteSpace(sqlServer))
        {
            return TypedResults.Conflict(Problems.SqlServerNotConfigured());
        }

        var result = await orchestrator.RequestAsync(
            infobase.Id, sqlServer, infobase.DatabaseName,
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
                b.FailureReason, b.ErrorMessage, FileAvailable: null))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

    // Живая сверка списка бэкапов с фактом на диске (MLC-178). Проверяем только Succeeded-строки
    // с непустым FilePath (Queued/Running/Failed/без пути — нечего сверять, FileAvailable=null).
    // Один батч FilesExistAsync на сервер (на практике сервер один — single-host MLC-088, но
    // строки хранят снимок DatabaseServer, поэтому группируем на всякий случай). Путь есть в
    // ответе словаря → его значение; пути нет (сервис не смог) → null = «не знаем». Viewer-read,
    // аудит НЕ пишем.
    private static async Task<List<BackupSummary>> ApplyFileAvailabilityAsync(
        ISqlBackupService backupService, List<BackupSummary> items, CancellationToken ct)
    {
        var checkable = items
            .Where(b => b.Status == BackupStatus.Succeeded && !string.IsNullOrEmpty(b.FilePath))
            .ToList();
        if (checkable.Count == 0)
        {
            return items;
        }

        var availabilityByPath = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var group in checkable.GroupBy(b => b.DatabaseServer, StringComparer.Ordinal))
        {
            var paths = group
                .Select(b => b.FilePath!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var existing = await backupService.FilesExistAsync(group.Key, paths, ct).ConfigureAwait(false);
            foreach (var pair in existing)
            {
                availabilityByPath[pair.Key] = pair.Value;
            }
        }

        return items
            .Select(b => b.Status == BackupStatus.Succeeded
                    && !string.IsNullOrEmpty(b.FilePath)
                    && availabilityByPath.TryGetValue(b.FilePath, out var available)
                ? b with { FileAvailable = available }
                : b)
            .ToList();
    }
}
