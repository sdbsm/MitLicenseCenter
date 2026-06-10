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
        var now = clock.GetUtcNow().UtcDateTime;
        if (refresh is true || !cache.TryGet(now, out var snapshot))
        {
            snapshot = await PollClusterAsync(cluster, loggerFactory, now, ct).ConfigureAwait(false);
            cache.Store(snapshot);
        }

        // Скрытые рендерятся из БД-снапшота (Name на момент скрытия) и отдаются всегда,
        // даже при Available:false — блок «Скрытые» в диалоге не зависит от RAS.
        var hiddenItems = await db.HiddenClusterInfobases.AsNoTracking()
            .OrderBy(x => x.Name).ThenBy(x => x.ClusterInfobaseId)
            .Select(x => new HiddenUnassignedInfobaseResponse(x.ClusterInfobaseId, x.Name, x.HiddenAtUtc, x.HiddenBy))
            .ToListAsync(ct).ConfigureAwait(false);

        if (!snapshot.Available)
        {
            // RAS недоступен — честный Available:false, а не пустой список (фронт по нему
            // прячет баннер и не показывает «ложный ноль»).
            return TypedResults.Ok(new UnassignedInfobasesResponse(
                Array.Empty<UnassignedInfobaseItemResponse>(),
                hiddenItems, Available: false, snapshot.Error, snapshot.CheckedAtUtc));
        }

        // Diff считается на каждый запрос (кэшируется только снапшот RAS): заведение,
        // скрытие и возврат базы видны сразу, без ожидания истечения TTL.
        var assigned = await db.Infobases.AsNoTracking()
            .Select(x => x.ClusterInfobaseId)
            .ToListAsync(ct).ConfigureAwait(false);
        var excluded = assigned.ToHashSet();
        excluded.UnionWith(hiddenItems.Select(h => h.ClusterInfobaseId));

        var items = snapshot.Infobases
            .Where(i => !excluded.Contains(i.Id))
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => new UnassignedInfobaseItemResponse(i.Id, i.Name, i.Description))
            .ToList();

        return TypedResults.Ok(new UnassignedInfobasesResponse(
            items, hiddenItems, Available: true, Error: null, snapshot.CheckedAtUtc));
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

    // Опрос RAS с санитизацией: адаптер при сбое rac.exe кладёт в Error сырой stderr
    // (может нести имена серверов/пути) — наружу он не уходит, только в лог. Исключение
    // адаптера (неожиданное — контракт ListInfobasesAsync «не бросает») страхуем тем же
    // паттерном, что discovery-эндпоинты; отмену (MLC-009) пробрасываем.
    private static async Task<UnassignedInfobasesCache.ClusterSnapshot> PollClusterAsync(
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
