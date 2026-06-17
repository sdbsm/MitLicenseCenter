using FluentAssertions;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Infrastructure.Backups.Testing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Backups;

// MLC-076: контракт программируемого фейка — на нём MLC-077 строит тесты оркестратора,
// поэтому запись аргументов и настраиваемые результаты проверяются здесь явно.
public sealed class FakeSqlBackupServiceTests
{
    [Fact]
    public async Task Backup_records_arguments_and_returns_configured_result()
    {
        var fake = new FakeSqlBackupService();
        var configured = new SqlBackupResult(
            false, BackupFailureReason.PermissionDenied, null, null, "нет sysadmin");
        fake.NextBackupResult = configured;

        var result = await fake.BackupAsync("sql.local", "acme_bp", @"D:\Backups", 2048, CancellationToken.None);

        result.Should().BeSameAs(configured);
        fake.BackupCalls.Should().ContainSingle()
            .Which.Should().Be(new FakeSqlBackupService.BackupCall("sql.local", "acme_bp", @"D:\Backups", 2048));
        fake.DeleteCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_records_arguments_and_returns_configured_result()
    {
        var fake = new FakeSqlBackupService();
        var cutoff = new DateTime(2026, 6, 9, 3, 15, 0, DateTimeKind.Utc);
        fake.NextDeleteResult = new SqlDeleteResult(false, "папка недоступна");

        var result = await fake.DeleteBackupsOlderThanAsync("sql.local", @"D:\Backups\acme_bp", cutoff, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("папка недоступна");
        fake.DeleteCalls.Should().ContainSingle()
            .Which.Should().Be(new FakeSqlBackupService.DeleteCall("sql.local", @"D:\Backups\acme_bp", cutoff));
    }

    [Fact]
    public async Task Estimate_records_arguments_and_returns_configured_result()
    {
        // MLC-183: предпоказ — фейк отдаёт NextEstimate и фиксирует вызов в EstimateCalls.
        var fake = new FakeSqlBackupService();
        var configured = new SqlBackupEstimate(
            EstimatedSizeBytes: 700L * 1024 * 1024,
            FreeSpaceBytes: 800L * 1024 * 1024,
            SafetyMarginBytes: 2048L * 1024 * 1024,
            Sufficient: false,
            Reason: BackupFailureReason.InsufficientSpace);
        fake.NextEstimate = configured;

        var result = await fake.EstimateAsync("sql.local", "acme_bp", @"D:\Backups", 2048, CancellationToken.None);

        result.Should().BeSameAs(configured);
        fake.EstimateCalls.Should().ContainSingle()
            .Which.Should().Be(new FakeSqlBackupService.EstimateCall("sql.local", "acme_bp", @"D:\Backups", 2048));
    }

    [Fact]
    public async Task Backup_gate_suspends_completion_until_signaled()
    {
        // Ворота — опора конкурентных тестов MLC-077 (потолок/per-db эксклюзия): бэкап
        // должен «висеть» выполняющимся, пока тест не отпустит.
        var fake = new FakeSqlBackupService();
        fake.BackupGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var pending = fake.BackupAsync("sql.local", "acme_bp", @"D:\Backups", 0, CancellationToken.None);

        pending.IsCompleted.Should().BeFalse("бэкап удерживается воротами до сигнала");
        fake.BackupCalls.Should().ContainSingle("вызов фиксируется сразу, до ворот");

        fake.BackupGate.SetResult();
        var result = await pending;
        result.Succeeded.Should().BeTrue();
    }
}
