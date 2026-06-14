using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Infrastructure.Backups;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Backups;

// MLC-152 (ADR-28, single-host): retention бэкапов теперь чисто файловая — .NET-перечисление
// «*.bak» + File.Delete вместо xp_delete_file (снят sysadmin-гейт). Юнитами проверяем отсечение
// по времени (cutoff), keep-latest-1, защиту чужих расширений и no-op на отсутствующем каталоге.
public sealed class BackupFileStoreTests : IDisposable
{
    private readonly string _folder;

    public BackupFileStoreTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "MlcBackupFileStoreTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Deletes_only_bak_files_older_than_cutoff()
    {
        var cutoff = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var stale = WriteBak("db_20260609_120000.bak", cutoff.AddHours(-2));
        var fresh = WriteBak("db_20260610_140000.bak", cutoff.AddHours(2));

        var deleted = BackupFileStore.DeleteBackupsOlderThan(_folder, cutoff, NullLogger.Instance);

        deleted.Should().Be(1);
        File.Exists(stale).Should().BeFalse("файл старше cutoff удаляется");
        File.Exists(fresh).Should().BeTrue("файл свежее cutoff остаётся");
    }

    [Fact]
    public void Keeps_file_exactly_at_cutoff()
    {
        // Строгое сравнение `<`: файл с временем РОВНО cutoff переживает (keep-latest-1 в
        // BackupAsync опирается на это — только что записанный .bak с временем старта/позже).
        var cutoff = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var atCutoff = WriteBak("db_at_cutoff.bak", cutoff);

        var deleted = BackupFileStore.DeleteBackupsOlderThan(_folder, cutoff, NullLogger.Instance);

        deleted.Should().Be(0);
        File.Exists(atCutoff).Should().BeTrue();
    }

    [Fact]
    public void Keep_latest_one_removes_all_previous_baks()
    {
        // Сценарий keep-latest-1 из BackupAsync: cutoff = момент старта нового бэкапа; все
        // прошлые .bak старше старта, новый — позже → остаётся ровно последний.
        var start = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        WriteBak("db_old1.bak", start.AddDays(-3));
        WriteBak("db_old2.bak", start.AddDays(-1));
        WriteBak("db_old3.bak", start.AddMinutes(-5));
        var latest = WriteBak("db_new.bak", start.AddSeconds(10));

        var deleted = BackupFileStore.DeleteBackupsOlderThan(_folder, start, NullLogger.Instance);

        deleted.Should().Be(3);
        Directory.EnumerateFiles(_folder, "*.bak").Should().ContainSingle()
            .Which.Should().Be(latest, "переживает ровно последний (созданный после старта)");
    }

    [Fact]
    public void Ignores_non_bak_files()
    {
        var cutoff = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var bak = WriteBak("db_old.bak", cutoff.AddDays(-1));
        var foreignTrn = WriteFile("db_old.trn", cutoff.AddDays(-1));
        var foreignTxt = WriteFile("readme.txt", cutoff.AddDays(-1));

        var deleted = BackupFileStore.DeleteBackupsOlderThan(_folder, cutoff, NullLogger.Instance);

        deleted.Should().Be(1);
        File.Exists(bak).Should().BeFalse();
        File.Exists(foreignTrn).Should().BeTrue("чужое расширение не трогаем");
        File.Exists(foreignTxt).Should().BeTrue("чужое расширение не трогаем");
    }

    [Fact]
    public void No_op_when_folder_missing()
    {
        var missing = Path.Combine(_folder, "does-not-exist");

        var deleted = BackupFileStore.DeleteBackupsOlderThan(
            missing, DateTime.UtcNow, NullLogger.Instance);

        deleted.Should().Be(0, "отсутствующий каталог — чистить нечего, no-op");
    }

    [Fact]
    public void Empty_folder_deletes_nothing()
    {
        var deleted = BackupFileStore.DeleteBackupsOlderThan(
            _folder, DateTime.UtcNow, NullLogger.Instance);

        deleted.Should().Be(0);
    }

    private string WriteBak(string name, DateTime lastWriteUtc) => WriteFile(name, lastWriteUtc);

    private string WriteFile(string name, DateTime lastWriteUtc)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, "stub");
        File.SetLastWriteTimeUtc(path, DateTime.SpecifyKind(lastWriteUtc, DateTimeKind.Utc));
        return path;
    }
}
