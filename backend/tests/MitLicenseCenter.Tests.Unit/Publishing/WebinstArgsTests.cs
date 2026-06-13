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

    // MLC-089 (single-host): адрес кластера деривируется из OneC.RAS.Endpoint —
    // отдельный ключ OneC.Cluster.Server снят. RAS host:port → host (порт RAS для
    // строки соединения с кластером не подходит).
    [Fact]
    public void ResolveClusterServer_takes_ras_host_without_port()
    {
        WebinstArgs.ResolveClusterServer("ras-host:1545").Should().Be("ras-host");
    }

    [Fact]
    public void ResolveClusterServer_uses_ras_as_is_when_no_port()
    {
        WebinstArgs.ResolveClusterServer("ras-host").Should().Be("ras-host");
    }

    [Fact]
    public void ResolveClusterServer_throws_when_ras_not_configured()
    {
        var act = () => WebinstArgs.ResolveClusterServer(null);
        act.Should().Throw<InvalidOperationException>();

        var actBlank = () => WebinstArgs.ResolveClusterServer("  ");
        actBlank.Should().Throw<InvalidOperationException>();
    }

    // MLC-117: с засеянным дефолтом OneC.RAS.Endpoint = "localhost:1545" публикация
    // строит валидный connStr без ошибки «Не задан адрес 1С-кластера» — порт RAS
    // отсекается, остаётся host (localhost), кластер слушает свой порт по умолчанию.
    [Fact]
    public void Seeded_default_endpoint_resolves_to_localhost_and_builds_connstr()
    {
        var server = WebinstArgs.ResolveClusterServer("localhost:1545");
        server.Should().Be("localhost");

        WebinstArgs.BuildConnStr(server, "Acme").Should().Be("Srvr=localhost;Ref=Acme;");
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

    // MLC-113: снятие — тот же набор, что и публикация, но с -delete вместо -publish.
    [Fact]
    public void BuildUnpublish_produces_delete_flags()
    {
        var args = WebinstArgs.BuildUnpublish(Pub(), @"C:\inetpub\wwwroot\acme", "Srvr=1c-srv;Ref=Acme;");

        args.Should().ContainInOrder("-delete", "-iis", "-wsdir", "acme", "-dir", @"C:\inetpub\wwwroot\acme", "-connstr", "Srvr=1c-srv;Ref=Acme;");
        args.Should().NotContain("-publish");
    }
}
