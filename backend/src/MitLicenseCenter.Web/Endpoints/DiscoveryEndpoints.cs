using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

// Discovery-эндпоинты для интерактивной настройки форм: вместо ручного ввода
// оператор выбирает значения из списков, которые приложение строит из источников
// истины (кластер 1С, SQL-сервер, IIS, файловая система). Admin-only, как /settings.
// Каждый ответ несёт флаг Available — фронт по нему решает показать ручной fallback.
public static class DiscoveryEndpoints
{
    public static void MapDiscoveryEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/discovery")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Discovery")
            .RequireAuthorization(Roles.Admin);

        group.MapGet("/cluster-infobases", GetClusterInfobasesAsync);
        group.MapGet("/databases", GetDatabasesAsync);
        group.MapGet("/iis-sites", GetIisSitesAsync);
        group.MapGet("/rac-paths", GetRacPaths);
        group.MapGet("/platform-versions", GetPlatformVersions);
    }

    internal static async Task<Ok<DiscoveryResponse<ClusterInfobaseDto>>> GetClusterInfobasesAsync(
        [FromServices] IClusterClient cluster,
        CancellationToken ct)
    {
        var result = await cluster.ListInfobasesAsync(ct).ConfigureAwait(false);
        var items = result.Infobases
            .Select(i => new ClusterInfobaseDto(i.Id, i.Name, i.Description))
            .ToList();
        return TypedResults.Ok(
            new DiscoveryResponse<ClusterInfobaseDto>(items, result.Available, result.Error));
    }

    internal static async Task<Ok<DiscoveryResponse<string>>> GetDatabasesAsync(
        [FromQuery] string? server,
        [FromServices] ISqlDatabaseDiscovery discovery,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            return TypedResults.Ok(new DiscoveryResponse<string>(
                Array.Empty<string>(), Available: false, Error: "Не указан сервер БД."));
        }

        try
        {
            var databases = await discovery.ListDatabasesAsync(server, ct).ConfigureAwait(false);
            return TypedResults.Ok(new DiscoveryResponse<string>(databases, Available: true, Error: null));
        }
        catch (Exception ex)
        {
            // Сервер недоступен / нет прав / неверное имя — фронт покажет ручной ввод.
            return TypedResults.Ok(new DiscoveryResponse<string>(
                Array.Empty<string>(), Available: false, Error: ex.Message));
        }
    }

    internal static async Task<Ok<DiscoveryResponse<IisSiteDto>>> GetIisSitesAsync(
        [FromServices] IIisPublishingService iis,
        CancellationToken ct)
    {
        try
        {
            var sites = await iis.ListSitesAsync(ct).ConfigureAwait(false);
            var items = sites.Select(s => new IisSiteDto(s.SiteName)).ToList();
            return TypedResults.Ok(new DiscoveryResponse<IisSiteDto>(items, Available: true, Error: null));
        }
        catch (Exception ex)
        {
            return TypedResults.Ok(new DiscoveryResponse<IisSiteDto>(
                Array.Empty<IisSiteDto>(), Available: false, Error: ex.Message));
        }
    }

    internal static Ok<DiscoveryResponse<string>> GetRacPaths(
        [FromServices] IRacPathDiscovery discovery)
    {
        var paths = discovery.FindRacExecutables();
        return TypedResults.Ok(new DiscoveryResponse<string>(paths, Available: true, Error: null));
    }

    internal static Ok<DiscoveryResponse<PlatformVersionDto>> GetPlatformVersions(
        [FromServices] IPlatformVersionDiscovery discovery)
    {
        var versions = discovery.FindPlatformVersions()
            .Select(v => new PlatformVersionDto(v.Version, v.Architecture))
            .ToList();
        return TypedResults.Ok(
            new DiscoveryResponse<PlatformVersionDto>(versions, Available: true, Error: null));
    }
}

public sealed record DiscoveryResponse<T>(IReadOnlyList<T> Items, bool Available, string? Error);

public sealed record ClusterInfobaseDto(Guid Id, string Name, string? Description);

public sealed record IisSiteDto(string SiteName);

public sealed record PlatformVersionDto(string Version, string? Architecture);
