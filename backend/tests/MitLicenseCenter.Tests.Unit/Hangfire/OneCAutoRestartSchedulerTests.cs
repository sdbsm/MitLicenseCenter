using FluentAssertions;
using MitLicenseCenter.Web.Hangfire;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Scheduling;

// MLC-218 (ADR-55): построение дневного cron из времени HH:mm для рекуррентной джобы
// авто-рестарта. Cron-формат Hangfire — "m H * * *" (минута, час, далее *). Мусорное/пустое
// время → false (вызывающий Apply трактует как «снять задание» вместо падения).
public sealed class OneCAutoRestartSchedulerTests
{
    [Theory]
    [InlineData("04:00", "0 4 * * *")]
    [InlineData("00:00", "0 0 * * *")]
    [InlineData("23:59", "59 23 * * *")]
    [InlineData("4:5", "5 4 * * *")] // ведущие нули опциональны на входе
    public void Builds_daily_cron_from_valid_time(string time, string expectedCron)
    {
        OneCAutoRestartScheduler.TryBuildCron(time, out var cron).Should().BeTrue();
        cron.Should().Be(expectedCron);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("24:00")] // час вне диапазона
    [InlineData("12:60")] // минута вне диапазона
    [InlineData("12")] // нет минут
    [InlineData("noon")] // не время
    public void Rejects_invalid_time(string? time)
    {
        OneCAutoRestartScheduler.TryBuildCron(time, out var cron).Should().BeFalse();
        cron.Should().BeEmpty();
    }
}
