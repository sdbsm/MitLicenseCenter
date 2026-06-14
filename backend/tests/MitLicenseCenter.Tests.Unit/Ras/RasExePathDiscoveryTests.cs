using FluentAssertions;
using MitLicenseCenter.Infrastructure.Ras;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Ras;

// Discovery ras.exe рядом с rac.exe в версионных bin-каталогах 1С (MLC-159).
public sealed class RasExePathDiscoveryTests : IDisposable
{
    private readonly string _root;

    public RasExePathDiscoveryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mlc-ras-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void CreateRasExe(string version)
    {
        var binDir = Path.Combine(_root, version, "bin");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "ras.exe"), "stub");
    }

    [Fact]
    public void ResolveForVersion_returns_path_for_installed_version()
    {
        CreateRasExe("8.5.1.1302");

        var path = RasExePathDiscovery.ResolveForVersion(new[] { _root }, "8.5.1.1302");

        path.Should().Be(Path.Combine(_root, "8.5.1.1302", "bin", "ras.exe"));
    }

    [Fact]
    public void ResolveForVersion_null_when_version_not_installed()
    {
        CreateRasExe("8.5.1.1302");

        RasExePathDiscovery.ResolveForVersion(new[] { _root }, "8.3.23.1865").Should().BeNull();
    }

    [Fact]
    public void ResolveForVersion_null_for_blank_version()
    {
        RasExePathDiscovery.ResolveForVersion(new[] { _root }, "").Should().BeNull();
    }

    [Fact]
    public void ResolveForVersion_null_when_root_missing()
    {
        RasExePathDiscovery
            .ResolveForVersion(new[] { Path.Combine(_root, "nope") }, "8.5.1.1302")
            .Should().BeNull();
    }
}
