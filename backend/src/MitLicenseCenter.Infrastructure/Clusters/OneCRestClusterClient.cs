using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Typed-HttpClient адаптер к 1С Cluster Management REST API (v8.3+).
// Реализует IClusterClient напрямую; ResilientClusterClient инжектирует его
// как IClusterClient (через фабричную регистрацию в DI), чтобы тесты могли
// подставить любой fake primary без зависимости от HttpClient.
// Маршруты задокументированы в docs/DECISIONS.md ADR-3.1.
// Базовый URL и Basic-credentials читаются из ISettingsSnapshot на каждый вызов,
// чтобы изменение настроек вступало в силу без перезапуска.
internal sealed class OneCRestClusterClient : IClusterClient
{
    // IClusterClient = primary REST adapter. ResilientClusterClient инжектирует этот
    // класс через конкретный тип (из IHttpClientFactory), получает IClusterClient.
    private static readonly HashSet<string> LicenseConsumingAppIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "1CV8", "1CV8C", "WebClient", "Designer", "COMConnection",
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly ISettingsSnapshot _settings;

    public OneCRestClusterClient(HttpClient http, ISettingsSnapshot settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct)
    {
        var clusterId = await GetFirstClusterIdAsync(ct).ConfigureAwait(false);
        if (clusterId is null)
        {
            return Array.Empty<ClusterSession>();
        }

        using var request = BuildRequest(HttpMethod.Get, $"/rm/cluster/{clusterId}/session");
        using var response = await SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var dtos = await response.Content
            .ReadFromJsonAsync<List<OneCSessionDto>>(JsonOpts, ct)
            .ConfigureAwait(false) ?? [];

        return dtos
            .Select(dto => new ClusterSession(
                SessionId: Guid.Parse(dto.Session),
                ClusterInfobaseId: Guid.Parse(dto.Infobase),
                AppId: dto.AppId ?? string.Empty,
                UserName: dto.UserName ?? string.Empty,
                Host: dto.Host ?? string.Empty,
                ConsumesLicense: ResolveConsumesLicense(dto),
                StartedAtUtc: ParseUtc(dto.StartedAt)))
            .ToList();
    }

    public async Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct)
    {
        var clusterId = await GetFirstClusterIdAsync(ct).ConfigureAwait(false);
        if (clusterId is null)
        {
            return new KillSessionResult(Killed: false, AlreadyGone: true);
        }

        using var request = BuildRequest(
            HttpMethod.Delete,
            $"/rm/cluster/{clusterId}/session/{descriptor.SessionId:D}");
        using var response = await SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NoContent
            || response.StatusCode == HttpStatusCode.OK)
        {
            return new KillSessionResult(Killed: true, AlreadyGone: false);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new KillSessionResult(Killed: false, AlreadyGone: true);
        }

        response.EnsureSuccessStatusCode();
        return new KillSessionResult(Killed: false, AlreadyGone: false);
    }

    public async Task<ClusterPingResult> PingAsync(CancellationToken ct)
    {
        try
        {
            using var request = BuildRequest(HttpMethod.Get, "/rm/cluster");
            using var response = await SendAsync(request, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new ClusterPingResult(Ok: true, Error: null);
            }
            return new ClusterPingResult(Ok: false, Error: $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or TaskCanceledException)
        {
            return new ClusterPingResult(Ok: false, Error: ex.Message);
        }
    }

    private async Task<string?> GetFirstClusterIdAsync(CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Get, "/rm/cluster");
        using var response = await SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var clusters = await response.Content
            .ReadFromJsonAsync<List<OneCClusterDto>>(JsonOpts, ct)
            .ConfigureAwait(false);

        return clusters?.Count > 0 ? clusters[0].Cluster : null;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string relPath)
    {
        var baseUrl = (_settings.GetString(SettingKey.OneCClusterRestApiUrl) ?? "http://localhost:1541")
            .TrimEnd('/');
        var uri = new Uri($"{baseUrl}{relPath}");
        var request = new HttpRequestMessage(method, uri);

        var user = _settings.GetString(SettingKey.OneCClusterAdminUser);
        var password = _settings.GetString(SettingKey.OneCClusterAdminPassword);
        if (user is not null && password is not null)
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        return request;
    }

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var timeoutSeconds = _settings.GetInt(SettingKey.OneCClusterRestApiTimeoutSeconds) ?? 5;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return _http.SendAsync(request, timeoutCts.Token);
    }

    private static bool ResolveConsumesLicense(OneCSessionDto dto)
    {
        if (dto.License?.Present.HasValue == true)
        {
            return dto.License.Present.Value;
        }
        return LicenseConsumingAppIds.Contains(dto.AppId ?? string.Empty);
    }

    private static DateTime ParseUtc(string? raw)
    {
        if (raw is null)
        {
            return DateTime.UtcNow;
        }
        if (DateTime.TryParse(raw, null,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
        return DateTime.UtcNow;
    }

    // --- Internal DTOs (1С Cluster REST API shapes, see ADR-3.1) ---

    private sealed class OneCClusterDto
    {
        [JsonPropertyName("cluster")]
        public string Cluster { get; set; } = string.Empty;

        [JsonPropertyName("host")]
        public string? Host { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class OneCSessionDto
    {
        [JsonPropertyName("session")]
        public string Session { get; set; } = string.Empty;

        [JsonPropertyName("infobase")]
        public string Infobase { get; set; } = string.Empty;

        [JsonPropertyName("user-name")]
        public string? UserName { get; set; }

        [JsonPropertyName("app-id")]
        public string? AppId { get; set; }

        [JsonPropertyName("host")]
        public string? Host { get; set; }

        [JsonPropertyName("started-at")]
        public string? StartedAt { get; set; }

        [JsonPropertyName("hibernate")]
        public bool Hibernate { get; set; }

        [JsonPropertyName("license")]
        public OneCLicenseDto? License { get; set; }
    }

    private sealed class OneCLicenseDto
    {
        [JsonPropertyName("present")]
        public bool? Present { get; set; }
    }
}
