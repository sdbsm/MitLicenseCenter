using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-092 — «нераспределённые» базы кластера: discovery-first разбор баз, которые есть
// в кластере 1С, но не заведены в панель (их сеансы молча отбрасывает ReconciliationJob
// и они не считаются ни в чей лимит). Admin-only, как discovery (Viewer не видит ничего
// нового — endpoint закрыт). IClusterClient инжектится напрямую — прецедент
// DiscoveryEndpoints.GetClusterInfobasesAsync (ADR-20: интерфейс-адаптер, не rac.exe).
public static partial class UnassignedInfobasesEndpoints
{
    public static void MapUnassignedInfobasesEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/infobases/unassigned")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Infobases")
            .RequireAuthorization(Roles.Admin);

        group.MapGet("/", GetUnassignedAsync);
        group.MapPost("/{clusterInfobaseId:guid}/hide", HideAsync);
        group.MapDelete("/{clusterInfobaseId:guid}/hide", UnhideAsync);
    }

    // Наружу при сбое RAS уходит только этот текст; сырая ошибка (stderr rac.exe может
    // нести имена серверов/пути) — в журнал сервера (паттерн discovery, MLC-009).
    private const string RasUnavailableError =
        "Не удалось получить список баз кластера 1С. Проверьте доступность сервера администрирования (RAS) и настройки 1С в разделе «Параметры».";

    // Сервисы — явный [FromServices] (стиль BackupsEndpoints): метаданные маршрутов
    // строятся без DI-инференса, что позволяет декларативный авторизационный тест.
    internal static async Task<Ok<UnassignedInfobasesResponse>> GetUnassignedAsync(
        [FromServices] AppDbContext db,
        [FromServices] IClusterClient cluster,
        [FromServices] UnassignedInfobasesCache cache,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] TimeProvider clock,
        [FromQuery] bool? refresh,
        CancellationToken ct)
    {
        var snapshot = await GetClusterSnapshotAsync(
            cluster, cache, loggerFactory, clock, refresh, ct).ConfigureAwait(false);

        // Скрытые рендерятся из БД-снапшота (Name на момент скрытия) и отдаются всегда,
        // даже при Available:false — блок «Скрытые» в диалоге не зависит от RAS.
        var hiddenItems = await db.HiddenClusterInfobases.AsNoTracking()
            .OrderBy(x => x.Name).ThenBy(x => x.ClusterInfobaseId)
            .Select(x => new HiddenUnassignedInfobaseResponse(x.ClusterInfobaseId, x.Name, x.HiddenAtUtc, x.HiddenBy))
            .ToListAsync(ct).ConfigureAwait(false);

        if (!snapshot.Available)
        {
            // RAS недоступен — честный Available:false, а не пустой список (фронт по нему
            // прячет баннер и не показывает «ложный ноль»). MissingItems тоже пуст: сбой
            // опроса RAS ≠ пропавшие базы, ложных красных меток быть не должно (MLC-095).
            return TypedResults.Ok(new UnassignedInfobasesResponse(
                Array.Empty<UnassignedInfobaseItemResponse>(),
                hiddenItems, Array.Empty<MissingInfobaseDto>(),
                Available: false, snapshot.Error, snapshot.CheckedAtUtc));
        }

        // Записи панели с именем клиента — основа для обоих diff'ов (join по образцу
        // infobaseMap в ReconciliationJob). Diff считается на каждый запрос (кэшируется
        // только снапшот RAS): заведение, скрытие, возврат и удаление видны сразу.
        var panelInfobases = await db.Infobases.AsNoTracking()
            .Join(
                db.Tenants.AsNoTracking(),
                i => i.TenantId,
                t => t.Id,
                (i, t) => new PanelInfobaseRow(i.Id, t.Name, i.Name, i.ClusterInfobaseId))
            .ToListAsync(ct).ConfigureAwait(false);

        var excluded = panelInfobases.Select(p => p.ClusterInfobaseId).ToHashSet();
        excluded.UnionWith(hiddenItems.Select(h => h.ClusterInfobaseId));

        var items = snapshot.Infobases
            .Where(i => !excluded.Contains(i.Id))
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => new UnassignedInfobaseItemResponse(i.Id, i.Name, i.Description))
            .ToList();

        // Обратный diff: записи панели, чьего ClusterInfobaseId нет в снапшоте кластера.
        var clusterIds = snapshot.Infobases.Select(i => i.Id).ToHashSet();
        var missingItems = panelInfobases
            .Where(p => !clusterIds.Contains(p.ClusterInfobaseId))
            .OrderBy(p => p.TenantName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(p => new MissingInfobaseDto(p.InfobaseId, p.TenantName, p.Name, p.ClusterInfobaseId))
            .ToList();

        return TypedResults.Ok(new UnassignedInfobasesResponse(
            items, hiddenItems, missingItems, Available: true, Error: null, snapshot.CheckedAtUtc));
    }

    internal static async Task<Results<NoContent, ValidationProblem, Conflict<ProblemDetails>>> HideAsync(
        Guid clusterInfobaseId,
        [FromBody] HideUnassignedInfobaseRequest request,
        [FromServices] AppDbContext db,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        [FromServices] TimeProvider clock,
        CancellationToken ct)
    {
        // Гоча minimal API (CLAUDE.md): DataAnnotations в runtime не прогоняются —
        // пустоту и длину (nvarchar(200)) проверяем руками.
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(HideUnassignedInfobaseRequest.Name)] =
                    ["Имя базы кластера обязательно и не длиннее 200 символов."],
            });
        }

        // Заведённая в панель база «нераспределённой» не является — hide по ней означает
        // устаревший список у клиента.
        if (await db.Infobases.AnyAsync(x => x.ClusterInfobaseId == clusterInfobaseId, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.UnassignedAlreadyAssigned());
        }

        if (await db.HiddenClusterInfobases.AnyAsync(x => x.ClusterInfobaseId == clusterInfobaseId, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.UnassignedAlreadyHidden());
        }

        db.HiddenClusterInfobases.Add(new HiddenClusterInfobase
        {
            ClusterInfobaseId = clusterInfobaseId,
            Name = name,
            HiddenAtUtc = clock.GetUtcNow().UtcDateTime,
            HiddenBy = httpContext.ResolveInitiator(),
        });
        // MLC-004 — backstop на гонке двух hide: нарушение PK мапим в тот же 409,
        // что и happy-path-проверка выше.
        var conflict = await db.SaveWithUniquenessBackstopAsync(ct,
            (UniqueIndexViolation.HiddenClusterInfobasePk, Problems.UnassignedAlreadyHidden)).ConfigureAwait(false);
        if (conflict is not null)
        {
            return TypedResults.Conflict(conflict);
        }

        await httpContext.AuditAsync(audit, AuditActionType.UnassignedInfobaseHidden,
            init => AuditDescriptions.UnassignedInfobaseHidden(name, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    internal static async Task<Results<NoContent, NotFound>> UnhideAsync(
        Guid clusterInfobaseId,
        [FromServices] AppDbContext db,
        [FromServices] IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var hidden = await db.HiddenClusterInfobases
            .FirstOrDefaultAsync(x => x.ClusterInfobaseId == clusterInfobaseId, ct).ConfigureAwait(false);
        if (hidden is null)
        {
            return TypedResults.NotFound();
        }

        db.HiddenClusterInfobases.Remove(hidden);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.UnassignedInfobaseUnhidden,
            init => AuditDescriptions.UnassignedInfobaseUnhidden(hidden.Name, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    // Счётчики дрейфа панель↔кластер по уже полученному снапшоту RAS (MLC-186a, дашборд-алерт).
    // Зеркалит ту же exclusion-конвенцию, что GetUnassignedAsync (Items/MissingItems), но без
    // tenant-join и проекций в DTO — нужны только числа: unassigned = базы кластера не в
    // (панель ∪ скрытые); notInCluster = записи панели, чьего ClusterInfobaseId нет в кластере.
    // Зовётся только при snapshot.Available (вызывающая сторона проверяет): сбой опроса RAS ≠
    // пропавшие базы (MLC-095), для недоступного RAS счётчики null, а не ложный ноль.
    internal static async Task<(int Unassigned, int NotInCluster)> CountDriftAsync(
        AppDbContext db, UnassignedInfobasesCache.ClusterSnapshot snapshot, CancellationToken ct)
    {
        var panelClusterIds = await db.Infobases.AsNoTracking()
            .Select(i => i.ClusterInfobaseId).ToListAsync(ct).ConfigureAwait(false);
        var hiddenIds = await db.HiddenClusterInfobases.AsNoTracking()
            .Select(h => h.ClusterInfobaseId).ToListAsync(ct).ConfigureAwait(false);

        var excluded = panelClusterIds.ToHashSet();
        excluded.UnionWith(hiddenIds);
        var unassigned = snapshot.Infobases.Count(i => !excluded.Contains(i.Id));

        var clusterIds = snapshot.Infobases.Select(i => i.Id).ToHashSet();
        var notInCluster = panelClusterIds.Count(id => !clusterIds.Contains(id));

        return (unassigned, notInCluster);
    }

    // TTL-кэш снапшота RAS перед опросом: единая точка для GET /infobases/unassigned и
    // серверного фильтра «не найдена в кластере» на GET /infobases (MLC-150). Один спавн
    // rac.exe на TTL делится между обоими маршрутами — спавн-бюджет ADR-3.3 соблюдён.
    internal static async Task<UnassignedInfobasesCache.ClusterSnapshot> GetClusterSnapshotAsync(
        IClusterClient cluster,
        UnassignedInfobasesCache cache,
        ILoggerFactory loggerFactory,
        TimeProvider clock,
        bool? refresh,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (refresh is true || !cache.TryGet(now, out var snapshot))
        {
            snapshot = await PollClusterAsync(cluster, loggerFactory, now, ct).ConfigureAwait(false);
            cache.Store(snapshot);
        }

        return snapshot;
    }

    // Опрос RAS с санитизацией: адаптер при сбое rac.exe кладёт в Error сырой stderr
    // (может нести имена серверов/пути) — наружу он не уходит, только в лог. Исключение
    // адаптера (неожиданное — контракт ListInfobasesAsync «не бросает») страхуем тем же
    // паттерном, что discovery-эндпоинты; отмену (MLC-009) пробрасываем.
    // internal: серверный фильтр «не найдена в кластере» на GET /infobases (MLC-150)
    // переиспользует тот же снапшот через тот же кэш — без второго спавна rac.exe.
    internal static async Task<UnassignedInfobasesCache.ClusterSnapshot> PollClusterAsync(
        IClusterClient cluster,
        ILoggerFactory loggerFactory,
        DateTime nowUtc,
        CancellationToken ct)
    {
        try
        {
            var result = await cluster.ListInfobasesAsync(ct).ConfigureAwait(false);
            if (!result.Available)
            {
                LogUnassignedDiscoveryUnavailable(
                    loggerFactory.CreateLogger(typeof(UnassignedInfobasesEndpoints).FullName!),
                    result.Error);
                return new UnassignedInfobasesCache.ClusterSnapshot(
                    Array.Empty<ClusterInfobase>(), Available: false, RasUnavailableError, nowUtc);
            }

            return new UnassignedInfobasesCache.ClusterSnapshot(
                result.Infobases, Available: true, Error: null, nowUtc);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogUnassignedDiscoveryFailed(
                loggerFactory.CreateLogger(typeof(UnassignedInfobasesEndpoints).FullName!), ex);
            return new UnassignedInfobasesCache.ClusterSnapshot(
                Array.Empty<ClusterInfobase>(), Available: false, RasUnavailableError, nowUtc);
        }
    }
}

// Полная причина сбоя — в журнал сервера (source-gen logger, как в DiscoveryEndpoints);
// наружу уходит только санитизированный русский текст.
public static partial class UnassignedInfobasesEndpoints
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Unassigned: кластер 1С недоступен при опросе списка инфобаз: {Error}")]
    private static partial void LogUnassignedDiscoveryUnavailable(ILogger logger, string? error);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Unassigned: не удалось получить список инфобаз кластера 1С.")]
    private static partial void LogUnassignedDiscoveryFailed(ILogger logger, Exception ex);
}
