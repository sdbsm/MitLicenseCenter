using FluentAssertions;
using MitLicenseCenter.Application.Maintenance;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Maintenance;

// MLC-217: чистая классификация прогона под-плана обслуживания (без SQL). Ключевые правила:
// различение «по расписанию» vs «по запросу» (ручной под-план без расписания НЕ просрочен) +
// провал/успех/просрочка. Реальный разбор sysmaintplan_*/SQL Agent — integration-only.
public sealed class SubplanRunPolicyTests
{
    private static readonly DateTime Now = new(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Scheduled_never_run_is_overdue()
    {
        // Под-план С расписанием, истории нет → просрочен (запланирован, но не запускался).
        var outcome = SubplanRunPolicy.Classify(
            hasSchedule: true, lastOutcomeSucceeded: null, lastRunUtc: null, Now);

        outcome.Should().Be(MaintenanceRunOutcome.Overdue);
    }

    [Fact]
    public void Ondemand_never_run_is_neverrun_not_overdue()
    {
        // Под-план БЕЗ расписания (ручной «перестроение индекса»/«month»), истории нет → NeverRun,
        // НЕ просрочен — это норма, не алерт (урок из плана владельца).
        var outcome = SubplanRunPolicy.Classify(
            hasSchedule: false, lastOutcomeSucceeded: null, lastRunUtc: null, Now);

        outcome.Should().Be(MaintenanceRunOutcome.NeverRun);
    }

    [Fact]
    public void Failed_last_run_is_failed_regardless_of_schedule()
    {
        var scheduled = SubplanRunPolicy.Classify(
            hasSchedule: true, lastOutcomeSucceeded: false, lastRunUtc: Now.AddHours(-1), Now);
        var onDemand = SubplanRunPolicy.Classify(
            hasSchedule: false, lastOutcomeSucceeded: false, lastRunUtc: Now.AddHours(-1), Now);

        scheduled.Should().Be(MaintenanceRunOutcome.Failed);
        onDemand.Should().Be(MaintenanceRunOutcome.Failed);
    }

    [Fact]
    public void Recent_success_is_succeeded()
    {
        var outcome = SubplanRunPolicy.Classify(
            hasSchedule: true, lastOutcomeSucceeded: true, lastRunUtc: Now.AddHours(-1), Now);

        outcome.Should().Be(MaintenanceRunOutcome.Succeeded);
    }

    [Fact]
    public void Scheduled_stale_success_is_overdue()
    {
        // Успешный, но последний прогон старше порога (~26ч) при действующем расписании → отстал.
        var outcome = SubplanRunPolicy.Classify(
            hasSchedule: true, lastOutcomeSucceeded: true, lastRunUtc: Now.AddHours(-30), Now);

        outcome.Should().Be(MaintenanceRunOutcome.Overdue);
    }

    [Fact]
    public void Ondemand_stale_success_is_not_overdue()
    {
        // Ручной под-план, успешный давно — НЕ просрочен (расписания нет, отставать нечему).
        var outcome = SubplanRunPolicy.Classify(
            hasSchedule: false, lastOutcomeSucceeded: true, lastRunUtc: Now.AddHours(-300), Now);

        outcome.Should().Be(MaintenanceRunOutcome.Succeeded);
    }

    [Theory]
    [InlineData(MaintenanceRunOutcome.Failed, true)]
    [InlineData(MaintenanceRunOutcome.Overdue, true)]
    [InlineData(MaintenanceRunOutcome.Succeeded, false)]
    [InlineData(MaintenanceRunOutcome.NeverRun, false)]
    [InlineData(MaintenanceRunOutcome.Unknown, false)]
    public void IsAlerting_only_for_failed_or_overdue(MaintenanceRunOutcome outcome, bool expected)
    {
        SubplanRunPolicy.IsAlerting(outcome).Should().Be(expected);
    }
}
