using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Builder;

namespace MitLicenseCenter.Web.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var version = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "0.0.0";

        endpoints
            .MapGet("/api/v{version:apiVersion}/health", () => Results.Ok(new
            {
                status = "ok",
                version,
                utcNow = DateTime.UtcNow,
            }))
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Health")
            .AllowAnonymous();
    }
}
