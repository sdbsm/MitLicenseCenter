using FluentAssertions;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Domain.TechLog;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-237 (этап C): int-значения enum'ов «Дела» расследования ЗАМОРОЖЕНЫ — контракт с БД
// (HasConversion<int>), та же дисциплина, что у PerfRecording* и AuditActionType. Reflection-инвариант
// полноты ловит добавление члена без обновления freeze-таблицы. Заменяет прежние TechLogCollection*-freeze
// (сущность мигрирована в Investigation). Дополнительно фиксирует int-совместимость InvestigationScenario
// с Application.TechLogScenario (миграция данных переносит строку→int по имени).
public sealed class TechLogEnumFreezeTests
{
    [Theory]
    [InlineData(InvestigationStatus.Collecting, 0)]
    [InlineData(InvestigationStatus.Analyzing, 1)]
    [InlineData(InvestigationStatus.Completed, 2)]
    [InlineData(InvestigationStatus.Interrupted, 3)]
    [InlineData(InvestigationStatus.Failed, 4)]
    public void Status_int_values_are_stable(InvestigationStatus status, int expected)
        => ((int)status).Should().Be(expected);

    [Fact]
    public void Status_all_members_covered_by_freeze_table()
    {
        var expected = new Dictionary<InvestigationStatus, int>
        {
            { InvestigationStatus.Collecting, 0 },
            { InvestigationStatus.Analyzing, 1 },
            { InvestigationStatus.Completed, 2 },
            { InvestigationStatus.Interrupted, 3 },
            { InvestigationStatus.Failed, 4 },
        };

        var actual = Enum.GetValues<InvestigationStatus>();
        actual.Should().HaveCount(expected.Count);
        foreach (var member in actual)
        {
            expected.Should().ContainKey(member);
            ((int)member).Should().Be(expected[member]);
        }
    }

    [Theory]
    [InlineData(InvestigationStopReason.Manual, 0)]
    [InlineData(InvestigationStopReason.TimeLimit, 1)]
    [InlineData(InvestigationStopReason.DiskLimit, 2)]
    [InlineData(InvestigationStopReason.Error, 3)]
    public void StopReason_int_values_are_stable(InvestigationStopReason reason, int expected)
        => ((int)reason).Should().Be(expected);

    [Fact]
    public void StopReason_all_members_covered_by_freeze_table()
    {
        var expected = new Dictionary<InvestigationStopReason, int>
        {
            { InvestigationStopReason.Manual, 0 },
            { InvestigationStopReason.TimeLimit, 1 },
            { InvestigationStopReason.DiskLimit, 2 },
            { InvestigationStopReason.Error, 3 },
        };

        var actual = Enum.GetValues<InvestigationStopReason>();
        actual.Should().HaveCount(expected.Count);
        foreach (var member in actual)
        {
            expected.Should().ContainKey(member);
            ((int)member).Should().Be(expected[member]);
        }
    }

    [Theory]
    [InlineData(InvestigationScenario.Locks, 0)]
    [InlineData(InvestigationScenario.SlowQueries, 1)]
    [InlineData(InvestigationScenario.Exceptions, 2)]
    [InlineData(InvestigationScenario.GeneralSlow, 3)]
    [InlineData(InvestigationScenario.DbmsLocks, 4)]
    public void Scenario_int_values_are_stable(InvestigationScenario scenario, int expected)
        => ((int)scenario).Should().Be(expected);

    [Theory]
    [InlineData(FindingKind.ManagedLocks, 0)]
    [InlineData(FindingKind.SlowQueries, 1)]
    [InlineData(FindingKind.Exceptions, 2)]
    [InlineData(FindingKind.DbmsLocks, 3)]
    public void FindingKind_int_values_are_stable(FindingKind kind, int expected)
        => ((int)kind).Should().Be(expected);

    // Совместимость int Domain-сценария с Application-сценарием: миграция данных и адаптер
    // (TechLogCollectionService) переносят TechLogScenario → InvestigationScenario прямым кастом по int.
    [Theory]
    [InlineData(TechLogScenario.Locks, InvestigationScenario.Locks)]
    [InlineData(TechLogScenario.SlowQueries, InvestigationScenario.SlowQueries)]
    [InlineData(TechLogScenario.Exceptions, InvestigationScenario.Exceptions)]
    [InlineData(TechLogScenario.GeneralSlow, InvestigationScenario.GeneralSlow)]
    [InlineData(TechLogScenario.DbmsLocks, InvestigationScenario.DbmsLocks)]
    public void Scenario_int_compatible_with_application_TechLogScenario(
        TechLogScenario appScenario, InvestigationScenario domainScenario)
        => ((int)appScenario).Should().Be((int)domainScenario);
}
