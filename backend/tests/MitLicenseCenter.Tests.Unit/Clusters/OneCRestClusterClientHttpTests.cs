using System.Net;
using System.Text;
using FluentAssertions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Clusters;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// Проверяет корректность десериализации JSON-ответов 1С Cluster REST API
// через StubHttpMessageHandler без реальных HTTP-вызовов.
public sealed class OneCRestClusterClientHttpTests
{
    private const string ClusterId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private const string InfobaseId = "11111111-2222-3333-4444-555555555555";
    private const string SessionId = "66666666-7777-8888-9999-aaaaaaaaaaaa";

    // В raw string $$$""": {{{expr}}} = интерполяция, одиночная { = литеральная скобка
    private static readonly string ClusterListJson = $$"""
        [{"cluster":"{{ClusterId}}","host":"my-server","port":1541,"name":"Test Cluster"}]
        """;

    private static readonly string SessionListJson = $$"""
        [
          {
            "session": "{{SessionId}}",
            "infobase": "{{InfobaseId}}",
            "user-name": "Иванов Иван",
            "app-id": "1CV8C",
            "host": "WORKSTATION01",
            "started-at": "2024-06-15T10:30:00",
            "hibernate": false,
            "license": {"present": true}
          },
          {
            "session": "77777777-8888-9999-aaaa-bbbbbbbbbbbb",
            "infobase": "{{InfobaseId}}",
            "user-name": "Service",
            "app-id": "BackgroundJob",
            "host": "SERVER01",
            "started-at": "2024-06-15T08:00:00",
            "hibernate": false,
            "license": {"present": false}
          }
        ]
        """;

    [Fact]
    public async Task ListActiveSessionsAsync_deserializes_sessions_correctly()
    {
        var client = BuildClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/rm/cluster", StringComparison.OrdinalIgnoreCase))
            {
                return OkJson(ClusterListJson);
            }
            if (path.Contains("/session", StringComparison.OrdinalIgnoreCase))
            {
                return OkJson(SessionListJson);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var sessions = await client.ListActiveSessionsAsync(default);

        sessions.Should().HaveCount(2);

        var first = sessions.First(s => s.SessionId == Guid.Parse(SessionId));
        first.ClusterInfobaseId.Should().Be(Guid.Parse(InfobaseId));
        first.AppId.Should().Be("1CV8C");
        first.UserName.Should().Be("Иванов Иван");
        first.Host.Should().Be("WORKSTATION01");
        first.ConsumesLicense.Should().BeTrue("license.present = true");
        first.StartedAtUtc.Kind.Should().Be(DateTimeKind.Utc, "UTC-нормализация обязательна");
        first.StartedAtUtc.Should().Be(new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc));

        var bg = sessions.First(s => s.AppId == "BackgroundJob");
        bg.ConsumesLicense.Should().BeFalse("license.present = false");
    }

    [Fact]
    public async Task ListActiveSessionsAsync_uses_license_present_over_appid_heuristic()
    {
        // app-id "1CV8C" normally ConsumesLicense=true, but license.present=false overrides.
        var json = $$"""
            [
              {
                "session": "{{SessionId}}",
                "infobase": "{{InfobaseId}}",
                "app-id": "1CV8C",
                "user-name": "Test",
                "host": "PC",
                "started-at": "2024-01-01T00:00:00",
                "license": {"present": false}
              }
            ]
            """;

        var client = BuildClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return path.EndsWith("/rm/cluster", StringComparison.OrdinalIgnoreCase)
                ? OkJson(ClusterListJson)
                : OkJson(json);
        });

        var sessions = await client.ListActiveSessionsAsync(default);

        sessions.Single().ConsumesLicense.Should().BeFalse(
            "license.present=false имеет приоритет над app-id эвристикой");
    }

    [Fact]
    public async Task ListActiveSessionsAsync_falls_back_to_appid_heuristic_when_license_absent()
    {
        var json = $$"""
            [
              {
                "session": "{{SessionId}}",
                "infobase": "{{InfobaseId}}",
                "app-id": "WebClient",
                "user-name": "Test",
                "host": "PC",
                "started-at": "2024-01-01T00:00:00"
              }
            ]
            """;

        var client = BuildClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return path.EndsWith("/rm/cluster", StringComparison.OrdinalIgnoreCase)
                ? OkJson(ClusterListJson)
                : OkJson(json);
        });

        var sessions = await client.ListActiveSessionsAsync(default);

        sessions.Single().ConsumesLicense.Should().BeTrue(
            "WebClient входит в эвристический набор license-consuming app-id");
    }

    [Fact]
    public async Task KillSessionAsync_returns_AlreadyGone_on_404()
    {
        var client = BuildClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var descriptor = new SessionDescriptor(
            Guid.Parse(InfobaseId), Guid.Parse(SessionId), "1CV8C", DateTime.UtcNow);

        var result = await client.KillSessionAsync(descriptor, default);

        result.Killed.Should().BeFalse();
        result.AlreadyGone.Should().BeTrue();
    }

    [Fact]
    public async Task KillSessionAsync_returns_Killed_on_204()
    {
        var client = BuildClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return path.EndsWith("/rm/cluster", StringComparison.OrdinalIgnoreCase)
                ? OkJson(ClusterListJson)
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var descriptor = new SessionDescriptor(
            Guid.Parse(InfobaseId), Guid.Parse(SessionId), "1CV8C", DateTime.UtcNow);

        var result = await client.KillSessionAsync(descriptor, default);

        result.Killed.Should().BeTrue();
        result.AlreadyGone.Should().BeFalse();
    }

    [Fact]
    public async Task PingAsync_returns_Ok_true_on_200()
    {
        var client = BuildClient(_ => OkJson(ClusterListJson));

        var ping = await client.PingAsync(default);

        ping.Ok.Should().BeTrue();
        ping.Error.Should().BeNull();
    }

    // ---

    private static OneCRestClusterClient BuildClient(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler));

        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCClusterRestApiUrl).Returns("http://test-server:1541");
        settings.GetString(SettingKey.OneCClusterAdminUser).Returns("admin");
        settings.GetString(SettingKey.OneCClusterAdminPassword).Returns("password");
        settings.GetInt(SettingKey.OneCClusterRestApiTimeoutSeconds).Returns(5);

        return new OneCRestClusterClient(httpClient, settings);
    }

    private static HttpResponseMessage OkJson(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
