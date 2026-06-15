using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Application.Updates;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-176 — хендлеры /updates/status (Viewer) и /updates/check-now (Admin). Прямой
// вызов internal static хендлера с реальным MemoryCache, фиктивным TimeProvider и
// подменённым IGitHubReleaseClient (стиль 1, как HealthReadyEndpointTests).
public sealed class UpdatesEndpointTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 16, 10, 0, 0, DateTimeKind.Utc);

    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    private static ISettingsSnapshot Settings(int enabled = 1, string? repo = "sdbsm/MitLicenseCenter", int intervalHours = 6)
    {
        var s = Substitute.For<ISettingsSnapshot>();
        s.GetInt(SettingKey.UpdatesEnabled).Returns(enabled);
        s.GetInt(SettingKey.UpdatesCheckIntervalHours).Returns(intervalHours);
        s.GetString(SettingKey.UpdatesRepository).Returns(repo);
        return s;
    }

    private static IGitHubReleaseClient Client(LatestReleaseInfo? release)
    {
        var client = Substitute.For<IGitHubReleaseClient>();
        client.GetLatestReleaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(release);
        return client;
    }

    private static UpdateStatusResponse Body(Ok<UpdateStatusResponse> result) => result.Value!;

    [Fact]
    public async Task Latest_higher_yields_update_available_with_fields()
    {
        var client = Client(new LatestReleaseInfo(
            "v999.0.0",
            "https://github.com/sdbsm/MitLicenseCenter/releases/tag/v999.0.0",
            "https://example/Setup.exe"));

        var result = await UpdatesEndpoints.StatusAsync(
            client, Settings(), NewCache(), TestHelpers.FixedClock(FixedNow), CancellationToken.None);

        var body = Body(result);
        body.UpdateAvailable.Should().BeTrue();
        body.CheckAvailable.Should().BeTrue();
        body.LatestVersion.Should().Be("v999.0.0");
        body.ReleaseUrl.Should().Be("https://github.com/sdbsm/MitLicenseCenter/releases/tag/v999.0.0");
        body.DownloadUrl.Should().Be("https://example/Setup.exe");
        body.CurrentVersion.Should().NotBeNullOrEmpty();
        body.CheckedAtUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Equal_or_older_latest_yields_no_update()
    {
        // Текущая версия сборки (CurrentVersion) точно не «ниже» 0.0.1 — обновления нет.
        var client = Client(new LatestReleaseInfo("0.0.1", "https://example/r", null));

        var result = await UpdatesEndpoints.StatusAsync(
            client, Settings(), NewCache(), TestHelpers.FixedClock(FixedNow), CancellationToken.None);

        var body = Body(result);
        body.UpdateAvailable.Should().BeFalse();
        body.CheckAvailable.Should().BeTrue();
        body.LatestVersion.Should().Be("0.0.1");
    }

    [Fact]
    public async Task Client_null_yields_check_unavailable()
    {
        var result = await UpdatesEndpoints.StatusAsync(
            Client(null), Settings(), NewCache(), TestHelpers.FixedClock(FixedNow), CancellationToken.None);

        var body = Body(result);
        body.CheckAvailable.Should().BeFalse();
        body.UpdateAvailable.Should().BeFalse();
        body.LatestVersion.Should().BeNull();
        body.ReleaseUrl.Should().BeNull();
        body.DownloadUrl.Should().BeNull();
        body.CurrentVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Disabled_does_not_call_github()
    {
        var client = Client(new LatestReleaseInfo("v999.0.0", "https://example/r", null));

        var result = await UpdatesEndpoints.StatusAsync(
            client, Settings(enabled: 0), NewCache(), TestHelpers.FixedClock(FixedNow), CancellationToken.None);

        Body(result).CheckAvailable.Should().BeFalse();
        await client.DidNotReceive().GetLatestReleaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_repository_does_not_call_github()
    {
        var client = Client(new LatestReleaseInfo("v999.0.0", "https://example/r", null));

        var result = await UpdatesEndpoints.StatusAsync(
            client, Settings(repo: ""), NewCache(), TestHelpers.FixedClock(FixedNow), CancellationToken.None);

        Body(result).CheckAvailable.Should().BeFalse();
        await client.DidNotReceive().GetLatestReleaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Status_caches_within_ttl_one_github_call()
    {
        var client = Client(new LatestReleaseInfo("v999.0.0", "https://example/r", null));
        var settings = Settings();
        var cache = NewCache();
        var clock = TestHelpers.FixedClock(FixedNow);

        await UpdatesEndpoints.StatusAsync(client, settings, cache, clock, CancellationToken.None);
        await UpdatesEndpoints.StatusAsync(client, settings, cache, clock, CancellationToken.None);

        await client.Received(1).GetLatestReleaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckNow_resets_cache_and_recomputes()
    {
        var client = Client(new LatestReleaseInfo("v999.0.0", "https://example/r", null));
        var settings = Settings();
        var cache = NewCache();
        var clock = TestHelpers.FixedClock(FixedNow);

        // Первый status кэширует результат.
        await UpdatesEndpoints.StatusAsync(client, settings, cache, clock, CancellationToken.None);
        // check-now сбрасывает кэш → клиент дёргается второй раз.
        await UpdatesEndpoints.CheckNowAsync(client, settings, cache, clock, CancellationToken.None);

        await client.Received(2).GetLatestReleaseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
