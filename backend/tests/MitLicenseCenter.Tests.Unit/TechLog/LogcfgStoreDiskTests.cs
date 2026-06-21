using FluentAssertions;
using MitLicenseCenter.Infrastructure.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-231 (60_SAFETY №3): измерение свободного места и размера каталога сбора — реальные ФС-методы
// LogcfgStore за seam'ом ILogcfgStore (директорию принимают параметром, не зависят от OneCInstallRoots).
// Только эти два метода трогают настоящую ФС в управляемом temp-каталоге; остальной store покрыт
// сервис-тестами через FakeLogcfgStore.
public sealed class LogcfgStoreDiskTests
{
    [Fact]
    public void GetAvailableFreeSpaceBytes_returns_positive_for_existing_volume()
    {
        var store = new LogcfgStore();

        var free = store.GetAvailableFreeSpaceBytes(Path.GetTempPath());

        free.Should().NotBeNull();
        free!.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetAvailableFreeSpaceBytes_returns_null_for_unresolvable_path()
    {
        var store = new LogcfgStore();

        // Несуществующий том → null (= «проверка невозможна», старт не блокируем).
        var free = store.GetAvailableFreeSpaceBytes(@"Z:\no-such-volume-mlc231\techlog");

        free.Should().BeNull();
    }

    [Fact]
    public void GetDirectorySizeBytes_sums_all_files_recursively()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mlc231-size-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "a.json"), new byte[1000]);
            var sub = Path.Combine(dir, "sub");
            Directory.CreateDirectory(sub);
            File.WriteAllBytes(Path.Combine(sub, "b.json"), new byte[500]);

            var store = new LogcfgStore();

            store.GetDirectorySizeBytes(dir).Should().Be(1500);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void GetDirectorySizeBytes_returns_zero_for_missing_directory()
    {
        var store = new LogcfgStore();

        store.GetDirectorySizeBytes(Path.Combine(Path.GetTempPath(), $"mlc231-absent-{Guid.NewGuid():N}"))
            .Should().Be(0);
    }
}
