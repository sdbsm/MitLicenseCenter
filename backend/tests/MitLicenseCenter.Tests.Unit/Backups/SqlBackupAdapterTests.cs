using FluentAssertions;
using MitLicenseCenter.Infrastructure.Backups;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Backups;

// MLC-076/MLC-152: сам адаптер в части BACKUP/RESTORE VERIFYONLY — integration-only (T-SQL
// юнитами не покрывается, как SqlPerformanceProbe/SqlDatabaseDiscovery; проверит владелец на
// стенде живым бэкапом). Юнитами покрывается чистая файловая логика, которая по ADR-28
// (single-host) заменила расширенные процедуры xp_* (MLC-152): разбор корня локального диска
// для DriveInfo и проверка свободного места.
public sealed class SqlBackupAdapterTests
{
    [Theory]
    [InlineData(@"D:\Backups", @"D:\")]
    [InlineData(@"d:\Backups\подпапка", @"D:\")]
    [InlineData("E:/Backups", @"E:\")]
    public void TryGetLocalDriveRoot_accepts_local_drive_roots(string folderRoot, string expected)
    {
        SqlBackupAdapter.TryGetLocalDriveRoot(folderRoot).Should().Be(expected);
    }

    [Theory]
    [InlineData(@"\\nas\backups")] // UNC — корня диска нет, DriveInfo неприменим
    [InlineData(@"Backups\acme")]  // относительный путь
    [InlineData(@"D:")]            // нет разделителя после двоеточия
    [InlineData("")]
    [InlineData(@"1:\Backups")]    // не буква
    public void TryGetLocalDriveRoot_rejects_non_local_roots(string folderRoot)
    {
        SqlBackupAdapter.TryGetLocalDriveRoot(folderRoot).Should().BeNull();
    }

    [Fact]
    public void TryGetFreeSpaceMb_returns_value_for_existing_local_drive()
    {
        // Каталог теста заведомо на локальном диске → корень читается, DriveInfo готов.
        var folder = Path.Combine(Path.GetTempPath(), "MlcBackupTest");
        var free = SqlBackupAdapter.TryGetFreeSpaceMb(folder);

        free.Should().NotBeNull("корневой диск временного каталога локальный и смонтирован");
        free!.Value.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(@"\\nas\backups")]
    [InlineData(@"Backups\acme")]
    public void TryGetFreeSpaceMb_returns_null_for_non_local_root(string folderRoot)
    {
        SqlBackupAdapter.TryGetFreeSpaceMb(folderRoot).Should().BeNull(
            "без корня локального диска проверка места невозможна — раньше это отклонял xp_fixeddrives");
    }
}
