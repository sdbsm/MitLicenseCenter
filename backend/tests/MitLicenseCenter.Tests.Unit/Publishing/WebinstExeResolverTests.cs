using FluentAssertions;
using MitLicenseCenter.Infrastructure.Publishing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Publishing;

// MLC-045: резолвер пути к webinst.exe по версии платформы (…\1cv8\<версия>\bin\webinst.exe).
public sealed class WebinstExeResolverTests : IDisposable
{
    private readonly string _root;

    public WebinstExeResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mlc-webinst-" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void Resolve_finds_exe_in_version_bin_folder()
    {
        var bin = Path.Combine(_root, "8.5.1.1302", "bin");
        Directory.CreateDirectory(bin);
        var exe = Path.Combine(bin, "webinst.exe");
        File.WriteAllText(exe, string.Empty);

        WebinstExeResolver.Resolve("8.5.1.1302", new[] { _root }).Should().Be(exe);
    }

    [Fact]
    public void Resolve_returns_null_when_version_not_installed()
    {
        Directory.CreateDirectory(_root);
        WebinstExeResolver.Resolve("8.3.99.9999", new[] { _root }).Should().BeNull();
    }

    [Fact]
    public void Resolve_returns_null_for_empty_version()
    {
        WebinstExeResolver.Resolve("", new[] { _root }).Should().BeNull();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
