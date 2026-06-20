using FluentAssertions;
using MitLicenseCenter.Domain.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-230: int-значения enum'ов жизненного цикла ЗАМОРОЖЕНЫ — контракт с БД (HasConversion<int>),
// та же дисциплина, что у PerfRecording* (PerfRecordingEnumFreezeTests) и AuditActionType.
// Reflection-инвариант полноты ловит добавление члена без обновления freeze-таблицы.
public sealed class TechLogEnumFreezeTests
{
    [Theory]
    [InlineData(TechLogCollectionStatus.Active, 0)]
    [InlineData(TechLogCollectionStatus.Stopped, 1)]
    [InlineData(TechLogCollectionStatus.Interrupted, 2)]
    public void Status_int_values_are_stable(TechLogCollectionStatus status, int expected)
        => ((int)status).Should().Be(expected);

    [Fact]
    public void Status_all_members_covered_by_freeze_table()
    {
        var expected = new Dictionary<TechLogCollectionStatus, int>
        {
            { TechLogCollectionStatus.Active, 0 },
            { TechLogCollectionStatus.Stopped, 1 },
            { TechLogCollectionStatus.Interrupted, 2 },
        };

        var actual = Enum.GetValues<TechLogCollectionStatus>();
        actual.Should().HaveCount(expected.Count);
        foreach (var member in actual)
        {
            expected.Should().ContainKey(member);
            ((int)member).Should().Be(expected[member]);
        }
    }

    [Theory]
    [InlineData(TechLogCollectionStopReason.Manual, 0)]
    [InlineData(TechLogCollectionStopReason.TimeLimit, 1)]
    [InlineData(TechLogCollectionStopReason.DiskLimit, 2)]
    public void StopReason_int_values_are_stable(TechLogCollectionStopReason reason, int expected)
        => ((int)reason).Should().Be(expected);

    [Fact]
    public void StopReason_all_members_covered_by_freeze_table()
    {
        var expected = new Dictionary<TechLogCollectionStopReason, int>
        {
            { TechLogCollectionStopReason.Manual, 0 },
            { TechLogCollectionStopReason.TimeLimit, 1 },
            { TechLogCollectionStopReason.DiskLimit, 2 },
        };

        var actual = Enum.GetValues<TechLogCollectionStopReason>();
        actual.Should().HaveCount(expected.Count);
        foreach (var member in actual)
        {
            expected.Should().ContainKey(member);
            ((int)member).Should().Be(expected[member]);
        }
    }
}
