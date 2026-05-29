using FluentAssertions;
using MitLicenseCenter.Infrastructure.Clusters;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

public sealed class RasHealthStateTests
{
    [Fact]
    public void Default_state_optimistic_healthy_no_check_yet()
    {
        var state = new RasHealthState();

        var snap = state.GetSnapshot();

        snap.Healthy.Should().BeTrue();
        snap.LastCheckedAtUtc.Should().BeNull();
        snap.LastErrorMessage.Should().BeNull();
        snap.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void RecordSuccess_sets_healthy_and_timestamp()
    {
        var state = new RasHealthState();
        var before = DateTime.UtcNow;

        state.RecordSuccess();

        var snap = state.GetSnapshot();
        snap.Healthy.Should().BeTrue();
        snap.LastCheckedAtUtc.Should().NotBeNull().And.BeOnOrAfter(before);
        snap.LastErrorMessage.Should().BeNull();
        snap.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void RecordFailure_sets_unhealthy_and_increments_counter()
    {
        var state = new RasHealthState();
        var before = DateTime.UtcNow;

        state.RecordFailure("boom");

        var snap = state.GetSnapshot();
        snap.Healthy.Should().BeFalse();
        snap.LastCheckedAtUtc.Should().NotBeNull().And.BeOnOrAfter(before);
        snap.LastErrorMessage.Should().Be("boom");
        snap.ConsecutiveFailures.Should().Be(1);
    }

    [Fact]
    public void ConsecutiveFailures_accumulates_across_failures()
    {
        var state = new RasHealthState();

        state.RecordFailure("e1");
        state.RecordFailure("e2");
        state.RecordFailure("e3");

        var snap = state.GetSnapshot();
        snap.ConsecutiveFailures.Should().Be(3);
        snap.LastErrorMessage.Should().Be("e3");
    }

    [Fact]
    public void Failure_to_success_resets_error_and_counter()
    {
        var state = new RasHealthState();
        state.RecordFailure("e1");
        state.RecordFailure("e2");

        state.RecordSuccess();

        var snap = state.GetSnapshot();
        snap.Healthy.Should().BeTrue();
        snap.LastErrorMessage.Should().BeNull();
        snap.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void Concurrent_writes_keep_snapshot_internally_consistent()
    {
        // Smoke: 100 параллельных RecordSuccess не должны вылетать с торнованным
        // snapshot'ом (Healthy=false + ConsecutiveFailures=0 — невозможная комбинация
        // если read под тем же lock'ом, что и write).
        var state = new RasHealthState();

        Parallel.For(0, 100, _ => state.RecordSuccess());

        var snap = state.GetSnapshot();
        snap.Healthy.Should().BeTrue();
        snap.ConsecutiveFailures.Should().Be(0);
        snap.LastErrorMessage.Should().BeNull();
    }
}
