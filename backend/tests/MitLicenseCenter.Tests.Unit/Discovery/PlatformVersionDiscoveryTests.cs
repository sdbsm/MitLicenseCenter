using FluentAssertions;
using MitLicenseCenter.Infrastructure.Discovery;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Discovery;

public sealed class PlatformVersionDiscoveryTests : IDisposable
{
    private readonly string _x64;
    private readonly string _x86;

    public PlatformVersionDiscoveryTests()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "mlc-ver-" + Guid.NewGuid().ToString("N"));
        _x64 = Path.Combine(baseDir, "x64", "1cv8");
        _x86 = Path.Combine(baseDir, "x86", "1cv8");
        Directory.CreateDirectory(_x64);
        Directory.CreateDirectory(_x86);
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(Path.GetDirectoryName(_x64));
        if (root is not null && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateVersion(string root, string version)
    {
        Directory.CreateDirectory(Path.Combine(root, version, "bin"));
    }

    [Fact]
    public void Scan_returns_versions_sorted_descending_with_architecture()
    {
        CreateVersion(_x64, "8.3.23.1865");
        CreateVersion(_x64, "8.5.1.1302");
        CreateVersion(_x64, "8.3.24.1761");

        var found = PlatformVersionDiscovery.Scan(new[] { (_x64, "x64") });

        found.Select(v => v.Version).Should().Equal("8.5.1.1302", "8.3.24.1761", "8.3.23.1865");
        found.Should().OnlyContain(v => v.Architecture == "x64");
    }

    [Fact]
    public void Scan_merges_architectures_when_same_version_in_both_roots()
    {
        CreateVersion(_x64, "8.5.1.1302");
        CreateVersion(_x86, "8.5.1.1302");

        var found = PlatformVersionDiscovery.Scan(new[] { (_x64, "x64"), (_x86, "x86") });

        found.Should().ContainSingle();
        found[0].Version.Should().Be("8.5.1.1302");
        found[0].Architecture.Should().Be("x64, x86");
    }

    [Fact]
    public void Scan_distinguishes_x86_only_version()
    {
        CreateVersion(_x64, "8.5.1.1302");
        CreateVersion(_x86, "8.3.23.1865");

        var found = PlatformVersionDiscovery.Scan(new[] { (_x64, "x64"), (_x86, "x86") });

        found.Single(v => v.Version == "8.5.1.1302").Architecture.Should().Be("x64");
        found.Single(v => v.Version == "8.3.23.1865").Architecture.Should().Be("x86");
    }

    [Fact]
    public void Scan_skips_non_version_folders_and_versions_without_bin()
    {
        CreateVersion(_x64, "8.5.1.1302");
        Directory.CreateDirectory(Path.Combine(_x64, "common")); // не версия
        Directory.CreateDirectory(Path.Combine(_x64, "8.4.0.0")); // версия без bin

        var found = PlatformVersionDiscovery.Scan(new[] { (_x64, "x64") });

        found.Select(v => v.Version).Should().Equal("8.5.1.1302");
    }

    [Fact]
    public void Scan_returns_empty_for_missing_root()
    {
        var found = PlatformVersionDiscovery.Scan(new[] { (Path.Combine(_x64, "nope"), "x64") });

        found.Should().BeEmpty();
    }
}
