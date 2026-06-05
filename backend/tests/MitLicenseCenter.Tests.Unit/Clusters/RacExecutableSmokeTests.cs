using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Clusters;
using MitLicenseCenter.Tests.Unit.Diagnostics;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// Smoke-тест против живого rac.exe + ras.exe + 1С Server Agent. CI пропускает.
// Для запуска локально:
//   dotnet user-secrets set "Smoke:RasExePath" "C:\Program Files\1cv8\8.5.1.1302\bin\rac.exe" --project backend/src/MitLicenseCenter.Web
//   dotnet user-secrets set "Smoke:RasEndpoint" "localhost:1545" --project backend/src/MitLicenseCenter.Web
// Дополнительные требования:
//   - Запущена служба «1C:Enterprise 8.5 Server Agent (x86-64)» (порт 1540)
//   - Запущен ras.exe на 1545 (`ras.exe cluster localhost:1540 --port=1545`)
[Trait("Category", "Smoke")]
public sealed class RacExecutableSmokeTests
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddUserSecrets(typeof(MitLicenseCenter.Web.Program).Assembly)
        .AddEnvironmentVariables()
        .Build();

    [Fact]
    public async Task PingAsync_returns_Ok_against_real_ras()
    {
        var (exePath, endpoint) = ReadSmokeConfig();
        var client = BuildClient(exePath, endpoint);

        var result = await client.PingAsync(default);

        result.Ok.Should().BeTrue($"PingAsync вернул ошибку: {result.Error}");
    }

    [Fact]
    public async Task ListActiveSessionsAsync_returns_nonnull_list_against_real_ras()
    {
        var (exePath, endpoint) = ReadSmokeConfig();
        var client = BuildClient(exePath, endpoint);

        var sessions = await client.ListActiveSessionsAsync(default);

        sessions.Should().NotBeNull("RAS-адаптер никогда не возвращает null — пустой список ОК");
        foreach (var s in sessions)
        {
            s.SessionId.Should().NotBeEmpty();
            s.ClusterInfobaseId.Should().NotBeEmpty();
            s.StartedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        }
    }

    [Fact]
    public async Task KillSessionAsync_returns_AlreadyGone_for_random_uuid()
    {
        // Идемпотентный smoke: дергаем kill для случайного UUID, ожидаем
        // AlreadyGone=true. rac.exe пишет stderr в OEM code page parent-процесса
        // (на RU Windows = CP866), и runner декодит соответствующе — иначе
        // маркер «Сеанс с указанным идентификатором не найден» не матчился бы.
        var (exePath, endpoint) = ReadSmokeConfig();
        var client = BuildClient(exePath, endpoint);

        var result = await client.KillSessionAsync(
            new SessionDescriptor(
                ClusterInfobaseId: Guid.NewGuid(),
                SessionId: Guid.NewGuid(),
                AppId: "1CV8C",
                StartedAtUtc: DateTime.UtcNow),
            default);

        result.Killed.Should().BeFalse();
        result.AlreadyGone.Should().BeTrue();
    }

    private static (string ExePath, string Endpoint) ReadSmokeConfig()
    {
        var exePath = Config["Smoke:RasExePath"];
        var endpoint = Config["Smoke:RasEndpoint"];

        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new InvalidOperationException(
                "Smoke:RasExePath не задан в User Secrets. " +
                "Запустите: dotnet user-secrets set \"Smoke:RasExePath\" \"C:\\Program Files\\1cv8\\<ver>\\bin\\rac.exe\" --project backend/src/MitLicenseCenter.Web");
        }
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "localhost:1545";
        }

        return (exePath, endpoint);
    }

    private static RacExecutableRasClusterClient BuildClient(string exePath, string endpoint)
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCRasExePath).Returns(exePath);
        settings.GetString(SettingKey.OneCRasEndpoint).Returns(endpoint);
        settings.GetString(SettingKey.OneCClusterAdminUser).Returns(Config["Smoke:ClusterUser"]);
        settings.GetString(SettingKey.OneCClusterAdminPassword).Returns(Config["Smoke:ClusterPassword"]);

        return new RacExecutableRasClusterClient(
            runner: new SystemProcessRacRunner(TestMetrics.Rac()),
            settings: settings,
            uuidCache: new ClusterUuidCache(),
            logger: NullLogger<RacExecutableRasClusterClient>.Instance);
    }
}
