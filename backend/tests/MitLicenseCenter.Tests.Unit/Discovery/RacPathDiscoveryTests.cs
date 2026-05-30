using FluentAssertions;
using MitLicenseCenter.Infrastructure.Discovery;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Discovery;

public sealed class RacPathDiscoveryTests : IDisposable
{
    private readonly string _root;

    public RacPathDiscoveryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mlc-rac-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateRacExe(string version)
    {
        var binDir = Path.Combine(_root, version, "bin");
        Directory.CreateDirectory(binDir);
        var exe = Path.Combine(binDir, "rac.exe");
        File.WriteAllText(exe, "stub");
        return exe;
    }

    [Fact]
    public void Scan_finds_rac_across_version_folders()
    {
        var a = CreateRacExe("8.3.23.1865");
        var b = CreateRacExe("8.5.1.1302");

        var found = RacPathDiscovery.Scan(new[] { _root });

        found.Should().Contain(a).And.Contain(b).And.HaveCount(2);
    }

    [Fact]
    public void Scan_skips_version_folders_without_rac()
    {
        CreateRacExe("8.5.1.1302");
        Directory.CreateDirectory(Path.Combine(_root, "8.4.0.0", "bin")); // bin без rac.exe

        var found = RacPathDiscovery.Scan(new[] { _root });

        found.Should().ContainSingle();
    }

    [Fact]
    public void Scan_returns_empty_for_missing_root()
    {
        var found = RacPathDiscovery.Scan(new[] { Path.Combine(_root, "nope") });

        found.Should().BeEmpty();
    }

    [Fact]
    public void Scan_deduplicates_when_roots_overlap()
    {
        CreateRacExe("8.5.1.1302");

        var found = RacPathDiscovery.Scan(new[] { _root, _root });

        found.Should().ContainSingle();
    }
}
