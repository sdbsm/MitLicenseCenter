using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

public static class SessionsEndpoints
{
    public static void MapSessionsEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/sessions")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Sessions");

        group.MapGet("/snapshot", SnapshotAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("/{id:guid}/kill", KillAsync).RequireAuthorization(Roles.Admin);
    }

    internal static Ok<SessionsSnapshotResponse> SnapshotAsync(
        IActiveSessionSnapshotStore store,
        TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var payload = store.Current();

        var items = payload.Items.Select(e => new SessionSnapshotEntry(
            e.SessionId,
            e.ClusterInfobaseId,
            e.TenantId,
            e.TenantName,
            e.InfobaseName,
            e.AppId,
            e.UserName,
            e.Host,
            e.ConsumesLicense,
            e.StartedAtUtc,
            (int)(now - e.StartedAtUtc).TotalSeconds)).ToList();

        return TypedResults.Ok(new SessionsSnapshotResponse(
            Items: items,
            CapturedAt: payload.CapturedAtUtc,
            TookMs: payload.TookMs,
            Source: payload.Source));
    }

    internal static async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>, ProblemHttpResult>> KillAsync(
        Guid id,
        KillSessionRequest request,
        IActiveSessionSnapshotStore store,
        IClusterClient cluster,
        IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var snapshot = store.Current();
        var session = snapshot.Items.FirstOrDefault(s => s.SessionId == id);

        if (session is null)
            return TypedResults.NotFound();

        // Идемпотентный протокол (DECISIONS.md «Idempotent kill protocol», как в
        // KillEnforcer): перед kill'ом сверяем дескриптор с актуальными данными
        // кластера. Снапшот может устареть до 25с — если по тому же SessionId
        // кластер вернул другой (InfobaseId, AppID, StartedAt), это уже не тот
        // сеанс, что видел оператор; не убиваем чужой сеанс, просим обновить список.
        var fresh = await cluster.ListActiveSessionsAsync(ct).ConfigureAwait(false);
        var freshSession = fresh.FirstOrDefault(s => s.SessionId == id);

        if (freshSession is not null &&
            (freshSession.ClusterInfobaseId != session.ClusterInfobaseId ||
             freshSession.AppId != session.AppId ||
             freshSession.StartedAtUtc != session.StartedAtUtc))
        {
            return TypedResults.Conflict(Problems.SessionStale());
        }

        var descriptor = new SessionDescriptor(
            session.ClusterInfobaseId,
            session.SessionId,
            session.AppId,
            session.StartedAtUtc);

        var result = await cluster.KillSessionAsync(descriptor, ct).ConfigureAwait(false);

        // Аудит — неизменяемая запись истины. «Завершён оператором» пишем только
        // когда kill действительно состоялся (Killed) либо сеанс уже отсутствовал
        // в кластере (AlreadyGone — идемпотентный успех, как в KillEnforcer).
        // Если RAS недоступен/вернул ошибку (оба флага false) — НИЧЕГО не пишем,
        // отдаём 502, чтобы в аудите не было записи-лжи.
        if (!result.Killed && !result.AlreadyGone)
        {
            return TypedResults.Problem(Problems.ClusterUnavailable());
        }

        var initiator = httpContext.User.Identity?.Name ?? "Unknown";
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? string.Empty
            : $" Причина: {request.Reason.Trim()}.";

        await audit.LogAsync(
            AuditActionType.SessionKilled,
            initiator,
            $"Сеанс {session.SessionId:N} ({session.AppId}, пользователь {SessionDisplay.UserNameOrFallback(session.UserName)}) завершён оператором.{reason}",
            session.TenantId,
            AuditReason.ManualByAdmin,
            ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
