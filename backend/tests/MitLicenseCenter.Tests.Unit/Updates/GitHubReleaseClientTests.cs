using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Infrastructure.Updates;
using MitLicenseCenter.Tests.Unit.TestDoubles;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Updates;

// MLC-176 — GitHubReleaseClient против синтетических HTTP-ответов (StubHttpMessageHandler).
public sealed class GitHubReleaseClientTests
{
    private static GitHubReleaseClient MakeClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        return new GitHubReleaseClient(httpClient, NullLogger<GitHubReleaseClient>.Instance);
    }

    private const string ReleaseWithExe = """
        {
          "tag_name": "v0.8.0-beta",
          "html_url": "https://github.com/sdbsm/MitLicenseCenter/releases/tag/v0.8.0-beta",
          "assets": [
            { "name": "readme.txt", "browser_download_url": "https://example/readme.txt" },
            { "name": "MitLicenseCenter-Setup.exe", "browser_download_url": "https://example/Setup.exe" }
          ]
        }
        """;

    private const string ReleaseWithoutExe = """
        {
          "tag_name": "v0.8.0",
          "html_url": "https://github.com/sdbsm/MitLicenseCenter/releases/tag/v0.8.0",
          "assets": [
            { "name": "checksums.txt", "browser_download_url": "https://example/checksums.txt" }
          ]
        }
        """;

    [Fact]
    public async Task Success_with_exe_asset_parses_tag_url_and_installer()
    {
        var client = MakeClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK, ReleaseWithExe));

        var result = await client.GetLatestReleaseAsync("sdbsm/MitLicenseCenter", CancellationToken.None);

        result.Should().NotBeNull();
        result!.TagName.Should().Be("v0.8.0-beta");
        result.HtmlUrl.Should().Be("https://github.com/sdbsm/MitLicenseCenter/releases/tag/v0.8.0-beta");
        result.InstallerDownloadUrl.Should().Be("https://example/Setup.exe");
    }

    [Fact]
    public async Task Success_without_exe_asset_yields_null_installer()
    {
        var client = MakeClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK, ReleaseWithoutExe));

        var result = await client.GetLatestReleaseAsync("sdbsm/MitLicenseCenter", CancellationToken.None);

        result.Should().NotBeNull();
        result!.TagName.Should().Be("v0.8.0");
        result.InstallerDownloadUrl.Should().BeNull();
    }

    [Fact]
    public async Task Rate_limited_403_yields_null()
    {
        var client = MakeClient(StubHttpMessageHandler.Returning(HttpStatusCode.Forbidden, "rate limit exceeded"));

        var result = await client.GetLatestReleaseAsync("sdbsm/MitLicenseCenter", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Thrown_exception_yields_null()
    {
        var client = MakeClient(StubHttpMessageHandler.Throwing(new HttpRequestException("connection refused")));

        var result = await client.GetLatestReleaseAsync("sdbsm/MitLicenseCenter", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Malformed_json_yields_null()
    {
        var client = MakeClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK, "{ this is : not json"));

        var result = await client.GetLatestReleaseAsync("sdbsm/MitLicenseCenter", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Requests_latest_release_route_for_repo()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, ReleaseWithExe);
        var client = MakeClient(handler);

        await client.GetLatestReleaseAsync("sdbsm/MitLicenseCenter", CancellationToken.None);

        handler.LastRequestUri!.AbsoluteUri
            .Should().Be("https://api.github.com/repos/sdbsm/MitLicenseCenter/releases/latest");
    }
}
