using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// Stage 2: snapshot — заглушка. Сам факт того, что endpoint возвращает 200
// и пустой items[] — это контракт для frontend (UI Sessions page реализуется
// против него до того, как Stage 3 добавит реальный adapter).
public sealed class SessionsSnapshotStubTests
{
    [Fact]
    public void Snapshot_returns_Ok_with_empty_items_and_capturedAt_now()
    {
        var fixedNow = new DateTime(2026, 5, 19, 10, 30, 0, DateTimeKind.Utc);
        var clock = TestHelpers.FixedClock(fixedNow);

        var result = SessionsEndpoints.SnapshotAsync(clock);

        result.Should().BeOfType<Ok<SessionsSnapshotResponse>>();
        result.Value!.Items.Should().BeEmpty();
        result.Value.CapturedAt.Should().Be(fixedNow);
        result.Value.CapturedAt.Should().BeAfter(DateTime.MinValue);
        result.Value.TookMs.Should().Be(0);
    }
}
