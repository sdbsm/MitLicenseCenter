using MitLicenseCenter.Infrastructure.Publishing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Publishing;

/// <summary>
/// PR 4.1: VrdPathResolver — pure static helper, никакого IIS-контекста не нужно.
/// Полностью покрывает оба branch'а (override / convention) с граничными случаями.
/// </summary>
public sealed class VrdPathResolverTests
{
    private const string DefaultRoot = @"C:\inetpub\1c-publications";
    private const string SiteName = "Default Web Site";
    private const string VirtualPath = "/mitpro";

    // ── Override branch ──────────────────────────────────────────────────────

    [Fact]
    public void Resolve_WithCleanOverride_ReturnsOverridePlusVrd()
    {
        var result = VrdPathResolver.Resolve(
            physicalPathOverride: @"C:\inetpub\wwwroot\mitpro",
            defaultVrdRoot: DefaultRoot,
            siteName: SiteName,
            virtualPath: VirtualPath);

        Assert.Equal(@"C:\inetpub\wwwroot\mitpro\default.vrd", result);
    }

    [Fact]
    public void Resolve_WithTrailingBackslash_TrimsAndReturnsCleanPath()
    {
        var result = VrdPathResolver.Resolve(
            physicalPathOverride: @"C:\inetpub\wwwroot\mitpro\",
            defaultVrdRoot: DefaultRoot,
            siteName: SiteName,
            virtualPath: VirtualPath);

        Assert.Equal(@"C:\inetpub\wwwroot\mitpro\default.vrd", result);
    }

    [Fact]
    public void Resolve_WithTrailingForwardSlash_TrimsAndReturnsCleanPath()
    {
        var result = VrdPathResolver.Resolve(
            physicalPathOverride: @"C:\inetpub\wwwroot\mitpro/",
            defaultVrdRoot: DefaultRoot,
            siteName: SiteName,
            virtualPath: VirtualPath);

        Assert.Equal(@"C:\inetpub\wwwroot\mitpro\default.vrd", result);
    }

    [Fact]
    public void Resolve_WithUncOverride_ReturnsUncPlusVrd()
    {
        var result = VrdPathResolver.Resolve(
            physicalPathOverride: @"\\server\share\app",
            defaultVrdRoot: DefaultRoot,
            siteName: SiteName,
            virtualPath: VirtualPath);

        Assert.Equal(@"\\server\share\app\default.vrd", result);
    }

    // ── Convention fallback branch ───────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WithNullOrEmptyOverride_UsesConvention(string? overrideValue)
    {
        var result = VrdPathResolver.Resolve(
            physicalPathOverride: overrideValue,
            defaultVrdRoot: DefaultRoot,
            siteName: SiteName,
            virtualPath: VirtualPath);

        // Convention: {root}\{site}\{trimmedVp}\default.vrd
        Assert.Equal(@"C:\inetpub\1c-publications\Default Web Site\mitpro\default.vrd", result);
    }

    [Fact]
    public void Resolve_Convention_TrimsLeadingSlashFromVirtualPath()
    {
        var result = VrdPathResolver.Resolve(
            physicalPathOverride: null,
            defaultVrdRoot: DefaultRoot,
            siteName: "MySite",
            virtualPath: "/myapp");

        Assert.Equal(@"C:\inetpub\1c-publications\MySite\myapp\default.vrd", result);
    }

    [Fact]
    public void Resolve_Convention_ConvertsForwardSlashesToBackslashes()
    {
        // VirtualPath с несколькими сегментами через «/» — все преобразуются.
        var result = VrdPathResolver.Resolve(
            physicalPathOverride: null,
            defaultVrdRoot: DefaultRoot,
            siteName: "MySite",
            virtualPath: "/sub/app");

        Assert.Equal(@"C:\inetpub\1c-publications\MySite\sub\app\default.vrd", result);
    }
}
