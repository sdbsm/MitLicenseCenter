using FluentAssertions;
using MitLicenseCenter.Application.Performance;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Performance;

// MLC-135 (BE-13): int-значения Perf-enum ЗАМОРОЖЕНЫ — контракт с БД (HasConversion<int>),
// та же дисциплина, что у AuditActionType (AuditLogEnumMappingTests) и BackupModels (BackupModelsTests).
// Reflection-инвариант полноты гарантирует, что новый член, добавленный без обновления freeze-таблицы,
// ронял бы тест (паттерн BE-14 / AuditLogEnumMappingTests.EnumTableCountMatchesDeclaredMembers).
public sealed class PerfRecordingEnumFreezeTests
{
    // ── PerfRecordingStatus ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(PerfRecordingStatus.Active, 0)]
    [InlineData(PerfRecordingStatus.Stopped, 1)]
    [InlineData(PerfRecordingStatus.Interrupted, 2)]
    public void PerfRecordingStatus_int_values_are_stable(PerfRecordingStatus status, int expected)
    {
        ((int)status).Should().Be(expected);
    }

    // Reflection-инвариант полноты: словарь ожидаемых пар member→int должен покрывать
    // ВСЕ объявленные члены. Добавление нового члена без обновления словаря ронает тест.
    [Fact]
    public void PerfRecordingStatus_all_members_covered_by_freeze_table()
    {
        var expected = new Dictionary<PerfRecordingStatus, int>
        {
            { PerfRecordingStatus.Active,      0 },
            { PerfRecordingStatus.Stopped,     1 },
            { PerfRecordingStatus.Interrupted, 2 },
        };

        var actual = Enum.GetValues<PerfRecordingStatus>();

        actual.Should().HaveCount(expected.Count,
            "каждый член PerfRecordingStatus должен быть зафиксирован в freeze-таблице (BE-13)");

        foreach (var member in actual)
        {
            expected.Should().ContainKey(member,
                $"член {member} должен быть зафиксирован в freeze-таблице");
            ((int)member).Should().Be(expected[member],
                $"int-значение {member} должно совпадать с зафиксированным в freeze-таблице");
        }
    }

    // ── PerfRecordingStopReason ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(PerfRecordingStopReason.Manual, 0)]
    [InlineData(PerfRecordingStopReason.TimeLimit, 1)]
    [InlineData(PerfRecordingStopReason.SampleLimit, 2)]
    public void PerfRecordingStopReason_int_values_are_stable(PerfRecordingStopReason reason, int expected)
    {
        ((int)reason).Should().Be(expected);
    }

    // Reflection-инвариант полноты для PerfRecordingStopReason.
    [Fact]
    public void PerfRecordingStopReason_all_members_covered_by_freeze_table()
    {
        var expected = new Dictionary<PerfRecordingStopReason, int>
        {
            { PerfRecordingStopReason.Manual,      0 },
            { PerfRecordingStopReason.TimeLimit,   1 },
            { PerfRecordingStopReason.SampleLimit, 2 },
        };

        var actual = Enum.GetValues<PerfRecordingStopReason>();

        actual.Should().HaveCount(expected.Count,
            "каждый член PerfRecordingStopReason должен быть зафиксирован в freeze-таблице (BE-13)");

        foreach (var member in actual)
        {
            expected.Should().ContainKey(member,
                $"член {member} должен быть зафиксирован в freeze-таблице");
            ((int)member).Should().Be(expected[member],
                $"int-значение {member} должно совпадать с зафиксированным в freeze-таблице");
        }
    }
}
