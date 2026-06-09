using FluentAssertions;
using MitLicenseCenter.Application.Backups;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Backups;

// MLC-076: int-значения enum'ов бэкапа ЗАМОРОЖЕНЫ — контракт с БД (HasConversion<int>),
// та же дисциплина, что у AuditActionType (AuditLogEnumMappingTests).
public sealed class BackupModelsTests
{
    [Theory]
    [InlineData(BackupStatus.Queued, 0)]
    [InlineData(BackupStatus.Running, 1)]
    [InlineData(BackupStatus.Succeeded, 2)]
    [InlineData(BackupStatus.Failed, 3)]
    public void BackupStatus_int_values_are_stable(BackupStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }

    [Theory]
    [InlineData(BackupFailureReason.None, 0)]
    [InlineData(BackupFailureReason.InsufficientSpace, 1)]
    [InlineData(BackupFailureReason.EstimateUnavailable, 2)]
    [InlineData(BackupFailureReason.PermissionDenied, 3)]
    [InlineData(BackupFailureReason.BackupFailed, 4)]
    [InlineData(BackupFailureReason.Interrupted, 5)]
    public void BackupFailureReason_int_values_are_stable(BackupFailureReason reason, int expected)
    {
        ((int)reason).Should().Be(expected);
    }
}
