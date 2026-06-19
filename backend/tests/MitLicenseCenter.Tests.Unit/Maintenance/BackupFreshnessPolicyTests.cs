using FluentAssertions;
using MitLicenseCenter.Application.Maintenance;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Maintenance;

// MLC-216: чистый расчёт флага «устарел» свежести бэкапов — без SQL. Порог свежести FULL —
// фиксированная константа ~26ч (BackupFreshnessPolicy.FullFreshnessThreshold). Проверяем
// границу порога и отсутствие FULL.
public sealed class BackupFreshnessPolicyTests
{
    private static readonly DateTime Now = new(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Threshold_is_documented_26_hours()
    {
        // Зафиксировано в каноне docs/04_BACKEND.md (~26ч). Изменение порога — осознанное
        // (этот тест обязан моргнуть вместе с правкой канона).
        BackupFreshnessPolicy.FullFreshnessThreshold.Should().Be(TimeSpan.FromHours(26));
    }

    [Fact]
    public void IsStale_true_when_no_full_backup_ever()
    {
        // Нет ни одного FULL — база невосстановима, всегда «устарел» (DIFF/LOG не спасают).
        BackupFreshnessPolicy.IsStale(lastFullUtc: null, Now).Should().BeTrue();
    }

    [Fact]
    public void IsStale_false_for_fresh_full_within_threshold()
    {
        var lastFull = Now - TimeSpan.FromHours(25);
        BackupFreshnessPolicy.IsStale(lastFull, Now).Should().BeFalse("25ч < порога 26ч");
    }

    [Fact]
    public void IsStale_false_exactly_at_threshold_boundary()
    {
        // Ровно на пороге (26ч) — ещё свежо (строгое «старше порога» для устаревания).
        var lastFull = Now - TimeSpan.FromHours(26);
        BackupFreshnessPolicy.IsStale(lastFull, Now).Should().BeFalse("ровно порог — ещё не устарел");
    }

    [Fact]
    public void IsStale_true_just_past_threshold()
    {
        var lastFull = Now - (TimeSpan.FromHours(26) + TimeSpan.FromMinutes(1));
        BackupFreshnessPolicy.IsStale(lastFull, Now).Should().BeTrue("26ч1мин > порога 26ч");
    }
}
