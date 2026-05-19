using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Clusters;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// Smoke-тест против реального 1С Cluster. CI пропускает этот трейт.
// Для запуска локально: настроить User Secrets проекта MitLicenseCenter.Web:
//   dotnet user-secrets set "Smoke:ClusterUrl" "http://my-server:1541"
//   dotnet user-secrets set "Smoke:ClusterUser" "admin"
//   dotnet user-secrets set "Smoke:ClusterPassword" "secret"
[Trait("Category", "Smoke")]
public sealed class OneCRestClusterClientSmokeTests
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddUserSecrets(typeof(MitLicenseCenter.Web.Program).Assembly)
        .AddEnvironmentVariables()
        .Build();

    [Fact]
    public async Task PingAsync_returns_Ok_against_real_cluster()
    {
        var url = Config["Smoke:ClusterUrl"];
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                "Smoke:ClusterUrl не задан в User Secrets. " +
                "Запустите: dotnet user-secrets set \"Smoke:ClusterUrl\" \"http://my-server:1541\"");
        }

        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCClusterRestApiUrl).Returns(url);
        settings.GetString(SettingKey.OneCClusterAdminUser).Returns(Config["Smoke:ClusterUser"]);
        settings.GetString(SettingKey.OneCClusterAdminPassword).Returns(Config["Smoke:ClusterPassword"]);
        settings.GetInt(SettingKey.OneCClusterRestApiTimeoutSeconds).Returns(10);

        using var httpClient = new HttpClient();
        var client = new OneCRestClusterClient(httpClient, settings);

        var result = await client.PingAsync(default);

        result.Ok.Should().BeTrue($"PingAsync вернул ошибку: {result.Error}");
    }

    [Fact]
    public async Task ListActiveSessionsAsync_returns_nonnull_list_against_real_cluster()
    {
        var url = Config["Smoke:ClusterUrl"];
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Smoke:ClusterUrl не задан в User Secrets.");
        }

        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCClusterRestApiUrl).Returns(url);
        settings.GetString(SettingKey.OneCClusterAdminUser).Returns(Config["Smoke:ClusterUser"]);
        settings.GetString(SettingKey.OneCClusterAdminPassword).Returns(Config["Smoke:ClusterPassword"]);
        settings.GetInt(SettingKey.OneCClusterRestApiTimeoutSeconds).Returns(10);

        using var httpClient = new HttpClient();
        var client = new OneCRestClusterClient(httpClient, settings);

        var sessions = await client.ListActiveSessionsAsync(default);

        sessions.Should().NotBeNull("метод не должен возвращать null");
        // Список может быть пустым (нет активных сессий) — но не null.
        foreach (var s in sessions)
        {
            s.SessionId.Should().NotBeEmpty();
            s.ClusterInfobaseId.Should().NotBeEmpty();
            s.StartedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        }
    }
}
