using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Updates;

namespace MitLicenseCenter.Infrastructure.Updates;

// MLC-176 — реализация порта IGitHubReleaseClient поверх типизированного HttpClient
// (первый исходящий HTTP в проекте; конфигурация — DependencyInjection.AddHttpClient).
// Анонимный GET repos/{owner}/{repo}/releases/latest публичного репо. Любой сбой
// (сеть, HTTP-не-2xx, rate-limit, битый JSON) логируем в журнал сервера и отдаём
// наружу `null` = «проверка недоступна»; клиентскую отмену пробрасываем.
public sealed partial class GitHubReleaseClient(HttpClient httpClient, ILogger<GitHubReleaseClient> logger)
    : IGitHubReleaseClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LatestReleaseInfo?> GetLatestReleaseAsync(string ownerRepo, CancellationToken ct)
    {
        var requestUri = $"repos/{ownerRepo}/releases/latest";
        try
        {
            using var response = await httpClient.GetAsync(requestUri, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // 403 (rate-limit без токена), 404 (нет релизов / приватный репо), 5xx — всё
                // это «проверить нельзя сейчас», не ошибка приложения. Грубый статус в лог.
                LogNonSuccess(logger, (int)response.StatusCode, ownerRepo);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var release = await JsonSerializer
                .DeserializeAsync<ReleaseDto>(stream, JsonOptions, ct)
                .ConfigureAwait(false);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName) || string.IsNullOrWhiteSpace(release.HtmlUrl))
            {
                LogMalformed(logger, ownerRepo);
                return null;
            }

            var installerUrl = release.Assets?
                .FirstOrDefault(a =>
                    !string.IsNullOrWhiteSpace(a.Name)
                    && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))?
                .BrowserDownloadUrl;

            return new LatestReleaseInfo(release.TagName, release.HtmlUrl, installerUrl);
        }
        // Отмену самого запроса (клиент ушёл) пробрасываем; всё остальное — «недоступно».
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogRequestFailed(logger, ownerRepo, ex);
            return null;
        }
    }

    // Только нужные поля контракта GitHub Releases API. Имена snake_case задаём явно
    // через JsonPropertyName (web-defaults camelCase их не сматчит).
    private sealed record ReleaseDto(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("assets")] IReadOnlyList<AssetDto>? Assets);

    private sealed record AssetDto(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Проверка обновлений: GitHub вернул HTTP {StatusCode} для {Repo} — проверка недоступна.")]
    private static partial void LogNonSuccess(ILogger logger, int statusCode, string repo);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Проверка обновлений: ответ GitHub для {Repo} без tag_name/html_url — проверка недоступна.")]
    private static partial void LogMalformed(ILogger logger, string repo);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Проверка обновлений: запрос к GitHub для {Repo} не удался — проверка недоступна.")]
    private static partial void LogRequestFailed(ILogger logger, string repo, Exception ex);
}
