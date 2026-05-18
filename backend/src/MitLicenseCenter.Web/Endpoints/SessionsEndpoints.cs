using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
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
    }

    // Stage 2: заглушка. Stage 3 заменит handler на чтение через ICluster1CClient
    // с двухтемповой reconciliation-петлёй (hot 3–5s / cold 20–30s) — см. ADR-3/ADR-6.
    internal static Ok<SessionsSnapshotResponse> SnapshotAsync(TimeProvider clock)
    {
        var capturedAt = clock.GetUtcNow().UtcDateTime;
        return TypedResults.Ok(new SessionsSnapshotResponse(
            Items: Array.Empty<SessionSnapshotEntry>(),
            CapturedAt: capturedAt,
            TookMs: 0));
    }
}
