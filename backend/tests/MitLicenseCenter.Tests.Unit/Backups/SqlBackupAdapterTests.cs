using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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

    // MLC-178: FilesExistAsync «never throws». Реальный xp_fileexist — integration-only
    // (как BACKUP/VERIFY): здесь покрываем чистые ветки без обращения к SQL.
    [Fact]
    public async Task FilesExistAsync_returns_empty_for_empty_paths_without_touching_sql()
    {
        var adapter = NewAdapter(connectionString: @"Server=nonexistent;Database=master;");

        var result = await adapter.FilesExistAsync("nonexistent", [], CancellationToken.None);

        result.Should().BeEmpty("пустой список путей не обращается к SQL");
    }

    [Fact]
    public async Task FilesExistAsync_returns_empty_when_connection_string_missing()
    {
        // Нет ConnectionStrings:Default → деградация в пустой словарь («не знаем»), без броска.
        var adapter = NewAdapter(connectionString: null);

        var result = await adapter.FilesExistAsync("any", [@"D:\Backups\x.bak"], CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static SqlBackupAdapter NewAdapter(string? connectionString)
    {
        var settings = new Dictionary<string, string?>();
        if (connectionString is not null)
        {
            settings["ConnectionStrings:Default"] = connectionString;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new SqlBackupAdapter(
            configuration, TimeProvider.System, NullLogger<SqlBackupAdapter>.Instance);
    }
}
