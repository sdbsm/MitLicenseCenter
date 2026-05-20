using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
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

    internal static async Task<Results<NoContent, NotFound>> KillAsync(
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

        var descriptor = new SessionDescriptor(
            session.ClusterInfobaseId,
            session.SessionId,
            session.AppId,
            session.StartedAtUtc);

        await cluster.KillSessionAsync(descriptor, ct).ConfigureAwait(false);

        var initiator = httpContext.User.Identity?.Name ?? "Unknown";
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? string.Empty
            : $" Причина: {request.Reason.Trim()}.";

        await audit.LogAsync(
            AuditActionType.SessionKilled,
            initiator,
            $"Сеанс {session.SessionId:N} ({session.AppId}, пользователь {session.UserName}) завершён оператором.{reason}",
            session.TenantId,
            AuditReason.ManualByAdmin,
            ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
