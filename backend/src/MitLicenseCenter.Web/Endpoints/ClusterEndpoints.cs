using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

public static class ClusterEndpoints
{
    public static void MapClusterEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/cluster")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Cluster");

        group.MapGet("/status", GetStatusAsync).RequireAuthorization(Roles.Viewer);
    }

    internal static Ok<CircuitStatusResponse> GetStatusAsync(ICircuitStatusReader reader)
    {
        var status = reader.GetStatus();
        return TypedResults.Ok(new CircuitStatusResponse(
            State: status.State,
            LastTransitionAt: status.LastTransitionAtUtc,
            LastErrorMessage: status.LastErrorMessage,
            ActiveAdapter: status.ActiveAdapter));
    }
}
