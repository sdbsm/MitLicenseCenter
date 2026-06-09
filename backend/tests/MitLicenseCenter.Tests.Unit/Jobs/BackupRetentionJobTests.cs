using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Backups.Testing;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-077 (ADR-27): ночная TTL-очистка бэкапов. Файлы — server-side через шов
// ISqlBackupService (DeleteCalls фейка); строки — provider-portable батч на реальном
// реляционном провайдере (SQLite), как LicenseUsageRetentionJobTests.
public sealed class BackupRetentionJobTests
{
    private static readonly DateTime Now = new(2026, 6, 9, 3, 15, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Deletes_files_for_each_distinct_database_with_ttl_cutoff()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var fake = new FakeSqlBackupService();
        var settings = Settings(folder: @"D:\Backups", ttlHours: 48);

        using (var seed = sqlite.NewContext())
        {
            // Две строки одной базы → один DISTINCT-вызов; вторая база на другом сервере.
            seed.DatabaseBackups.AddRange(
                Row("SQL01", "alpha_db", BackupStatus.Succeeded, Now.AddHours(-60)),
                Row("SQL01", "alpha_db", BackupStatus.Failed, Now.AddHours(-1)),
                Row("SQL02", "beta_db", BackupStatus.Succeeded, Now.AddHours(-2)));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, fake, settings).RunAsync(CancellationToken.None);
        }

        var cutoff = Now.AddHours(-48);
        fake.DeleteCalls.Should().BeEquivalentTo(
        [
            new FakeSqlBackupService.DeleteCall("SQL01", Path.Combine(@"D:\Backups", "alpha_db"), cutoff),
            new FakeSqlBackupService.DeleteCall("SQL02", Path.Combine(@"D:\Backups", "beta_db"), cutoff),
        ], "по одному server-side вызову на каждую DISTINCT-пару server+db с cutoff = now − TTL");
    }

    [Fact]
    public async Task Clamps_ttl_to_definition_minimum()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var fake = new FakeSqlBackupService();
        var settings = Settings(folder: @"D:\Backups", ttlHours: 0); // ниже whitelist-минимума 1

        using (var seed = sqlite.NewContext())
        {
            seed.DatabaseBackups.Add(Row("SQL01", "alpha_db", BackupStatus.Succeeded, Now.AddHours(-2)));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, fake, settings).RunAsync(CancellationToken.None);
        }

        fake.DeleteCalls.Should().ContainSingle()
            .Which.CutoffUtc.Should().Be(Now.AddHours(-1), "TTL клампится к минимуму диапазона (1 час)");
    }

    [Fact]
    public async Task Purges_only_completed_rows_older_than_cutoff()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var fake = new FakeSqlBackupService();
        var settings = Settings(folder: @"D:\Backups", ttlHours: 24);
        var audit = new TestHelpers.CapturingAuditLogger();

        var oldSucceeded = Row("SQL01", "alpha_db", BackupStatus.Succeeded, Now.AddHours(-30));
        var oldFailed = Row("SQL01", "beta_db", BackupStatus.Failed, Now.AddHours(-30));
        var oldQueued = Row("SQL01", "gamma_db", BackupStatus.Queued, Now.AddHours(-30));
        var oldRunning = Row("SQL01", "delta_db", BackupStatus.Running, Now.AddHours(-30));
        var fresh = Row("SQL01", "epsilon_db", BackupStatus.Succeeded, Now.AddHours(-1));
        using (var seed = sqlite.NewContext())
        {
            seed.DatabaseBackups.AddRange(oldSucceeded, oldFailed, oldQueued, oldRunning, fresh);
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, fake, settings, audit).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.DatabaseBackups.Select(b => b.Id).ToList()
            .Should().BeEquivalentTo([oldQueued.Id, oldRunning.Id, fresh.Id],
                "живая очередь (Queued/Running) и свежие строки не трогаются");

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActionType.BackupsPurged);
        entry.Initiator.Should().Be("System");
        entry.Description.Should().Contain("2");
    }

    [Fact]
    public async Task Does_not_audit_when_nothing_was_deleted()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var fake = new FakeSqlBackupService();
        var settings = Settings(folder: @"D:\Backups", ttlHours: 24);
        var audit = new TestHelpers.CapturingAuditLogger();

        using (var seed = sqlite.NewContext())
        {
            seed.DatabaseBackups.Add(Row("SQL01", "alpha_db", BackupStatus.Succeeded, Now.AddHours(-1)));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, fake, settings, audit).RunAsync(CancellationToken.None);
        }

        audit.Entries.Should().BeEmpty("аудит пишется только когда удалена хотя бы одна строка");
    }

    [Fact]
    public async Task No_ops_when_folder_path_is_not_configured()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var fake = new FakeSqlBackupService();
        var settings = Settings(folder: null, ttlHours: 24);
        var audit = new TestHelpers.CapturingAuditLogger();

        using (var seed = sqlite.NewContext())
        {
            seed.DatabaseBackups.Add(Row("SQL01", "alpha_db", BackupStatus.Succeeded, Now.AddHours(-60)));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, fake, settings, audit).RunAsync(CancellationToken.None);
        }

        fake.DeleteCalls.Should().BeEmpty();
        audit.Entries.Should().BeEmpty();
        using var verify = sqlite.NewContext();
        verify.DatabaseBackups.Count().Should().Be(1, "без папки джоба — полный no-op");
    }

    // MLC-074 (класс дыры): прод включает EnableRetryOnFailure → ручная транзакция вне
    // CreateExecutionStrategy().ExecuteAsync падает. Дефолтный SQLite
    // (NonRetryingExecutionStrategy) этого не ловит — навешиваем ретраящую стратегию.
    [Fact]
    public async Task Purges_rows_under_a_retrying_execution_strategy()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create(
            o => o.ExecutionStrategy(d => new TestHelpers.RetriesOnFailureExecutionStrategy(d)));
        var fake = new FakeSqlBackupService();
        var settings = Settings(folder: @"D:\Backups", ttlHours: 24);

        var old = Row("SQL01", "alpha_db", BackupStatus.Succeeded, Now.AddHours(-30));
        var fresh = Row("SQL01", "alpha_db", BackupStatus.Succeeded, Now.AddHours(-1));
        using (var seed = sqlite.NewContext())
        {
            seed.DatabaseBackups.AddRange(old, fresh);
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, fake, settings).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.DatabaseBackups.Select(b => b.Id).ToList()
            .Should().BeEquivalentTo([fresh.Id], "reap отрабатывает и при ретраящей стратегии (MLC-074)");
    }

    private static BackupRetentionJob NewJob(
        AppDbContext db,
        FakeSqlBackupService fake,
        ISettingsSnapshot settings,
        TestHelpers.CapturingAuditLogger? audit = null) =>
        new(db, fake, audit ?? new TestHelpers.CapturingAuditLogger(), settings,
            TestHelpers.FixedClock(Now), NullLogger<BackupRetentionJob>.Instance);

    private static ISettingsSnapshot Settings(string? folder, int ttlHours)
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.BackupFolderPath).Returns(folder);
        settings.GetInt(SettingKey.BackupTtlHours).Returns(ttlHours);
        return settings;
    }

    private static DatabaseBackup Row(
        string server, string database, BackupStatus status, DateTime requestedAtUtc) => new()
        {
            Id = Guid.NewGuid(),
            InfobaseId = Guid.NewGuid(),
            DatabaseServer = server,
            DatabaseName = database,
            Status = status,
            RequestedBy = "operator",
            RequestedAtUtc = requestedAtUtc,
        };
}
