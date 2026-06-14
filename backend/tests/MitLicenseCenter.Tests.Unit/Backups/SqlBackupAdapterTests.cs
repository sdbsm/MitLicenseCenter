using FluentAssertions;
using MitLicenseCenter.Infrastructure.Backups;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Backups;

// MLC-076: сам адаптер — integration-only (T-SQL юнитами не покрывается, как
// SqlPerformanceProbe/SqlDatabaseDiscovery); юнитами проверяется только чистый
// разбор корня папки бэкапов (локальный диск — требование disk-guard'а ADR-27).
public sealed class SqlBackupAdapterTests
{
    [Theory]
    [InlineData(@"D:\Backups", 'D')]
    [InlineData(@"d:\Backups\подпапка", 'D')]
    [InlineData("E:/Backups", 'E')]
    public void TryGetDriveLetter_accepts_local_drive_roots(string folderRoot, char expected)
    {
        SqlBackupAdapter.TryGetDriveLetter(folderRoot, out var drive).Should().BeTrue();
        drive.Should().Be(expected);
    }

    [Theory]
    [InlineData(@"\\nas\backups")] // UNC — xp_fixeddrives его не видит, проверка места невозможна
    [InlineData(@"Backups\acme")]  // относительный путь
    [InlineData(@"D:")]            // нет разделителя после двоеточия
    [InlineData("")]
    [InlineData(@"1:\Backups")]    // не буква
    public void TryGetDriveLetter_rejects_non_local_roots(string folderRoot)
    {
        SqlBackupAdapter.TryGetDriveLetter(folderRoot, out _).Should().BeFalse();
    }
}
