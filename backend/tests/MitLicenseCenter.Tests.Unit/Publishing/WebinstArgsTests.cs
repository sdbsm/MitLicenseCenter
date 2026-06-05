using FluentAssertions;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Infrastructure.Publishing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Publishing;

// MLC-045: pure-хелпер сборки аргументов webinst и строки соединения.
public sealed class WebinstArgsTests
{
    private static Publication Pub(string virtualPath = "/acme", string? physicalOverride = null) => new()
    {
        Id = Guid.NewGuid(),
        SiteName = "Default Web Site",
        VirtualPath = virtualPath,
        PlatformVersion = "8.3.23.1865",
        PhysicalPathOverride = physicalOverride,
    };

    [Theory]
    [InlineData("/acme", "acme")]
    [InlineData("acme", "acme")]
    [InlineData("/acme/", "acme")]
    public void VirtualDirName_strips_slashes(string input, string expected)
    {
        WebinstArgs.VirtualDirName(input).Should().Be(expected);
    }

    [Fact]
    public void ResolveClusterServer_prefers_explicit_setting()
    {
        WebinstArgs.ResolveClusterServer("1c-srv:1541", "ras-host:1545").Should().Be("1c-srv:1541");
    }

    [Fact]
    public void ResolveClusterServer_falls_back_to_ras_host_without_port()
    {
        WebinstArgs.ResolveClusterServer(null, "ras-host:1545").Should().Be("ras-host");
    }

    [Fact]
    public void ResolveClusterServer_uses_ras_as_is_when_no_port()
    {
        WebinstArgs.ResolveClusterServer("  ", "ras-host").Should().Be("ras-host");
    }

    [Fact]
    public void ResolveClusterServer_throws_when_nothing_configured()
    {
        var act = () => WebinstArgs.ResolveClusterServer(null, null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuildConnStr_uses_server_and_infobase_name()
    {
        WebinstArgs.BuildConnStr("1c-srv", "Acme BP").Should().Be("Srvr=1c-srv;Ref=Acme BP;");
    }

    [Fact]
    public void BuildPublish_produces_expected_flags()
    {
        var args = WebinstArgs.BuildPublish(Pub(), @"C:\inetpub\wwwroot\acme", "Srvr=1c-srv;Ref=Acme;");

        args.Should().ContainInOrder("-publish", "-iis", "-wsdir", "acme", "-dir", @"C:\inetpub\wwwroot\acme", "-connstr", "Srvr=1c-srv;Ref=Acme;");
    }
}
