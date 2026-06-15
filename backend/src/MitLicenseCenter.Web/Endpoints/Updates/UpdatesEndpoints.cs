using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Application.Updates;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Domain.Updates;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-176 (ADR-50) — проверка обновлений через GitHub Releases. Панель ненавязчиво
// сигналит о новой версии: баннер всем ролям, ссылка на релиз всем, скачивание
// установщика и «Проверить сейчас» — Admin. Бэкенд только отдаёт URL установщика;
// запуск под UAC — руками админа. Аудит не пишем (enum заморожен). Без фонового
// hosted-service: ленивая проверка под IMemoryCache (как DashboardEndpoints).
public static class UpdatesEndpoints
{
    internal const string CacheKey = "updates:status";

    // Текущая версия панели — из informational-версии сборки (как HealthEndpoints).
    private static readonly string CurrentVersion = Assembly
        .GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "0.0.0";

    public static void MapUpdatesEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/updates")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Updates");

        group.MapGet("/status", StatusAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("/check-now", CheckNowAsync).RequireAuthorization(Roles.Admin);
    }

    // Ленивая проверка: результат кэшируется на Updates.CheckIntervalHours (как
    // dashboard:summary). При недоступной проверке — короткий TTL 5 мин, чтобы
    // временный сбой GitHub/сети не «залип» на часы.
    internal static async Task<Ok<UpdateStatusResponse>> StatusAsync(
        IGitHubReleaseClient gitHub,
        ISettingsSnapshot settings,
        IMemoryCache cache,
        TimeProvider clock,
        CancellationToken ct)
    {
        var response = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            var result = await ComputeAsync(gitHub, settings, clock, ct).ConfigureAwait(false);
            entry.AbsoluteExpirationRelativeToNow = result.CheckAvailable
                ? TimeSpan.FromHours(ResolveIntervalHours(settings))
                : TimeSpan.FromMinutes(5);
            return result;
        }).ConfigureAwait(false);

        return TypedResults.Ok(response!);
    }

    // Admin форсит свежую проверку: сбрасываем кэш и пересчитываем. Возвращаем
    // свежий статус (фронт кладёт его в queryClient, баннер обновляется сразу).
    internal static async Task<Ok<UpdateStatusResponse>> CheckNowAsync(
        IGitHubReleaseClient gitHub,
        ISettingsSnapshot settings,
        IMemoryCache cache,
        TimeProvider clock,
        CancellationToken ct)
    {
        cache.Remove(CacheKey);
        return await StatusAsync(gitHub, settings, cache, clock, ct).ConfigureAwait(false);
    }

    internal static async Task<UpdateStatusResponse> ComputeAsync(
        IGitHubReleaseClient gitHub,
        ISettingsSnapshot settings,
        TimeProvider clock,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        // Рубильник: 0 → в GitHub не ходим, проверка недоступна.
        var enabled = (settings.GetInt(SettingKey.UpdatesEnabled) ?? 1) != 0;
        if (!enabled)
        {
            return Unavailable(now);
        }

        var repository = settings.GetString(SettingKey.UpdatesRepository);
        if (string.IsNullOrWhiteSpace(repository))
        {
            return Unavailable(now);
        }

        var release = await gitHub.GetLatestReleaseAsync(repository.Trim(), ct).ConfigureAwait(false);
        if (release is null)
        {
            // Сеть/HTTP-сбой/rate-limit/битый JSON — клиент вернул null = «проверка недоступна».
            return Unavailable(now);
        }

        var updateAvailable = UpdateComparison.IsUpdateAvailable(CurrentVersion, release.TagName);

        return new UpdateStatusResponse(
            CurrentVersion: CurrentVersion,
            LatestVersion: release.TagName,
            UpdateAvailable: updateAvailable,
            ReleaseUrl: release.HtmlUrl,
            DownloadUrl: release.InstallerDownloadUrl,
            CheckAvailable: true,
            CheckedAtUtc: now);
    }

    private static UpdateStatusResponse Unavailable(DateTime now) => new(
        CurrentVersion: CurrentVersion,
        LatestVersion: null,
        UpdateAvailable: false,
        ReleaseUrl: null,
        DownloadUrl: null,
        CheckAvailable: false,
        CheckedAtUtc: now);

    private static int ResolveIntervalHours(ISettingsSnapshot settings) =>
        settings.GetInt(SettingKey.UpdatesCheckIntervalHours) is { } h && h >= 1 ? h : 6;
}
