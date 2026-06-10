using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

// Discovery-эндпоинты для интерактивной настройки форм: вместо ручного ввода
// оператор выбирает значения из списков, которые приложение строит из источников
// истины (кластер 1С, SQL-сервер, IIS, файловая система). Admin-only, как /settings.
// Каждый ответ несёт флаг Available — фронт по нему решает показать ручной fallback.
public static partial class DiscoveryEndpoints
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
        group.MapGet("/sql-instances", GetSqlInstances);
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

    // Single-host (MLC-087): сервер берётся из настройки Sql.Server, query-параметра нет.
    // Пустая настройка → Available:false с подсказкой задать сервер в «Параметрах».
    internal static async Task<Ok<DiscoveryResponse<string>>> GetDatabasesAsync(
        [FromServices] ISqlDatabaseDiscovery discovery,
        [FromServices] ISettingsSnapshot settings,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var server = settings.GetString(SettingKey.SqlServer);
        if (string.IsNullOrWhiteSpace(server))
        {
            return TypedResults.Ok(new DiscoveryResponse<string>(
                Array.Empty<string>(),
                Available: false,
                Error: "Сервер СУБД не задан. Укажите его в разделе «Параметры»."));
        }

        try
        {
            var databases = await discovery.ListDatabasesAsync(ct).ConfigureAwait(false);
            return TypedResults.Ok(new DiscoveryResponse<string>(databases, Available: true, Error: null));
        }
        // MLC-009: отмену запроса не выдаём за «ошибку discovery» — пробрасываем.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Сервер недоступен / нет прав / неверное имя. Полное исключение — в лог;
            // наружу только санитизированный русский текст (без имён серверов/SQL-деталей):
            // фронт по Available:false покажет ручной ввод.
            LogDatabaseDiscoveryFailed(loggerFactory.CreateLogger(typeof(DiscoveryEndpoints).FullName!), server, ex);
            return TypedResults.Ok(new DiscoveryResponse<string>(
                Array.Empty<string>(),
                Available: false,
                Error: "Не удалось получить список баз данных. Проверьте доступность SQL-сервера и права доступа или введите имя базы вручную."));
        }
    }

    internal static async Task<Ok<DiscoveryResponse<IisSiteDto>>> GetIisSitesAsync(
        [FromServices] IIisPublishingService iis,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        try
        {
            var sites = await iis.ListSitesAsync(ct).ConfigureAwait(false);
            var items = sites.Select(s => new IisSiteDto(s.SiteName)).ToList();
            return TypedResults.Ok(new DiscoveryResponse<IisSiteDto>(items, Available: true, Error: null));
        }
        // MLC-009: отмену запроса не выдаём за «ошибку discovery» — пробрасываем.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // COM/нет прав/IIS недоступен. Полное исключение — в лог; наружу только
            // санитизированный русский текст (без путей/имён сайтов).
            LogIisSitesDiscoveryFailed(loggerFactory.CreateLogger(typeof(DiscoveryEndpoints).FullName!), ex);
            return TypedResults.Ok(new DiscoveryResponse<IisSiteDto>(
                Array.Empty<IisSiteDto>(),
                Available: false,
                Error: "Не удалось получить список сайтов IIS. Проверьте доступность веб-сервера и права службы или введите имя сайта вручную."));
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

    // MLC-056: локальные инстансы SQL из реестра. Без query-параметра (localhost-only).
    // Чтение реестра синхронно и быстро, но может бросить (нет прав) — try/catch как
    // у GetIisSitesAsync: исключение в лог, наружу санитизированный текст + Available:false.
    internal static Ok<DiscoveryResponse<string>> GetSqlInstances(
        [FromServices] ISqlInstanceDiscovery discovery,
        [FromServices] ILoggerFactory loggerFactory)
    {
        try
        {
            var instances = discovery.FindLocalInstances();
            return TypedResults.Ok(new DiscoveryResponse<string>(instances, Available: true, Error: null));
        }
        catch (Exception ex)
        {
            // Реестр недоступен / нет прав. Полное исключение — в лог; наружу только
            // санитизированный русский текст. Фронт по Available:false покажет ручной ввод.
            LogSqlInstancesDiscoveryFailed(loggerFactory.CreateLogger(typeof(DiscoveryEndpoints).FullName!), ex);
            return TypedResults.Ok(new DiscoveryResponse<string>(
                Array.Empty<string>(),
                Available: false,
                Error: "Не удалось получить список инстансов SQL Server. Введите сервер БД вручную."));
        }
    }
}

// MLC-009: полное инфраструктурное исключение пишем в журнал сервера (source-gen
// logger, как в остальном коде); наружу уходит только санитизированный русский текст.
public static partial class DiscoveryEndpoints
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Discovery: не удалось получить список баз данных с SQL-сервера {Server}.")]
    private static partial void LogDatabaseDiscoveryFailed(ILogger logger, string server, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Discovery: не удалось получить список сайтов IIS.")]
    private static partial void LogIisSitesDiscoveryFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Discovery: не удалось получить список инстансов SQL Server из реестра.")]
    private static partial void LogSqlInstancesDiscoveryFailed(ILogger logger, Exception ex);
}

public sealed record DiscoveryResponse<T>(IReadOnlyList<T> Items, bool Available, string? Error);

public sealed record ClusterInfobaseDto(Guid Id, string Name, string? Description);

public sealed record IisSiteDto(string SiteName);

public sealed record PlatformVersionDto(string Version, string? Architecture);
