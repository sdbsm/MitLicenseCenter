using FluentAssertions;
using MitLicenseCenter.Infrastructure.Publishing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Publishing;

// MLC-045: pure-хелпер замены версии в пути к wsisapi.dll (web.config / default.vrd).
public sealed class WsisapiVersionRewriterTests
{
    private const string WebConfig =
        "<configuration><system.webServer><handlers>" +
        "<add name=\"1C Web-service Extension\" path=\"*\" verb=\"*\" modules=\"IsapiModule\" " +
        "scriptProcessor=\"C:\\Program Files\\1cv8\\8.3.23.1865\\bin\\wsisapi.dll\" resourceType=\"Unspecified\" />" +
        "</handlers></system.webServer></configuration>";

    [Fact]
    public void TryReadVersion_extracts_version_from_wsisapi_path()
    {
        WsisapiVersionRewriter.TryReadVersion(WebConfig).Should().Be("8.3.23.1865");
    }

    [Fact]
    public void TryReadVersion_returns_null_when_no_wsisapi_path()
    {
        WsisapiVersionRewriter.TryReadVersion("<configuration/>").Should().BeNull();
    }

    [Fact]
    public void TryReadVersion_handles_single_digit_build_8_5()
    {
        var content = "scriptProcessor=\"C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\wsisapi.dll\"";
        WsisapiVersionRewriter.TryReadVersion(content).Should().Be("8.5.1.1302");
    }

    [Fact]
    public void Rewrite_replaces_only_version_segment()
    {
        var patched = WsisapiVersionRewriter.Rewrite(WebConfig, "8.3.24.1234");

        patched.Should().Contain("1cv8\\8.3.24.1234\\bin\\wsisapi.dll");
        patched.Should().NotContain("8.3.23.1865");
        // Остальная структура файла не тронута.
        patched.Should().Contain("1C Web-service Extension");
    }

    [Fact]
    public void Rewrite_is_noop_when_no_wsisapi_path()
    {
        const string content = "<configuration><appSettings/></configuration>";
        WsisapiVersionRewriter.Rewrite(content, "8.3.24.1234").Should().Be(content);
    }

    [Fact]
    public void Rewrite_does_not_touch_unrelated_four_segment_numbers()
    {
        // Случайное «1.2.3.4» вне пути к wsisapi.dll не должно зацепиться.
        var content = "<x ver=\"1.2.3.4\"/>" + WebConfig;
        var patched = WsisapiVersionRewriter.Rewrite(content, "8.3.24.1234");

        patched.Should().Contain("ver=\"1.2.3.4\"");
        patched.Should().Contain("1cv8\\8.3.24.1234\\bin\\wsisapi.dll");
    }
}
