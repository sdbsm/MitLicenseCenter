using FluentAssertions;
using MitLicenseCenter.Infrastructure.Performance;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Performance;

// MLC-068: чистый расчёт дельт DMV-пробы (wait-stats / IO-stall кумулятивны с старта SQL →
// значимы как дельта между poll'ами, как CPU% host-пробы MLC-064) + признак 1С-SQL. Без БД.
public sealed class SqlPerformanceProbeDeltaTests
{
    // --- wait-stats дельта ---

    [Fact]
    public void ComputeWaitDeltas_returns_positive_growth_ordered_descending()
    {
        var previous = new Dictionary<string, SqlPerformanceProbe.WaitRaw>(StringComparer.Ordinal)
        {
            ["PAGEIOLATCH_SH"] = new("PAGEIOLATCH_SH", 1000, 10),
            ["LCK_M_X"] = new("LCK_M_X", 500, 5),
        };
        var current = new[]
        {
            new SqlPerformanceProbe.WaitRaw("PAGEIOLATCH_SH", 1200, 14), // +200ms, +4 задачи
            new SqlPerformanceProbe.WaitRaw("LCK_M_X", 2500, 9),         // +2000ms, +4 задачи
        };

        var deltas = SqlPerformanceProbe.ComputeWaitDeltas(previous, current, top: 10);

        deltas.Should().HaveCount(2);
        deltas[0].WaitType.Should().Be("LCK_M_X", "наибольший прирост времени ожидания идёт первым");
        deltas[0].WaitTimeMsDelta.Should().Be(2000);
        deltas[0].WaitingTasksDelta.Should().Be(4);
        deltas[1].WaitType.Should().Be("PAGEIOLATCH_SH");
        deltas[1].WaitTimeMsDelta.Should().Be(200);
    }

    [Fact]
    public void ComputeWaitDeltas_excludes_benign_idle_waits()
    {
        var previous = new Dictionary<string, SqlPerformanceProbe.WaitRaw>(StringComparer.Ordinal)
        {
            ["SLEEP_TASK"] = new("SLEEP_TASK", 1000, 1),
            ["LAZYWRITER_SLEEP"] = new("LAZYWRITER_SLEEP", 1000, 1),
            ["LCK_M_S"] = new("LCK_M_S", 100, 1),
        };
        var current = new[]
        {
            new SqlPerformanceProbe.WaitRaw("SLEEP_TASK", 9000, 9),         // доброкачественное — выкинуть
            new SqlPerformanceProbe.WaitRaw("LAZYWRITER_SLEEP", 9000, 9),   // доброкачественное — выкинуть
            new SqlPerformanceProbe.WaitRaw("LCK_M_S", 300, 3),
        };

        var deltas = SqlPerformanceProbe.ComputeWaitDeltas(previous, current, top: 10);

        deltas.Should().ContainSingle().Which.WaitType.Should().Be("LCK_M_S");
    }

    [Fact]
    public void ComputeWaitDeltas_skips_new_types_and_nonpositive_and_counter_reset()
    {
        var previous = new Dictionary<string, SqlPerformanceProbe.WaitRaw>(StringComparer.Ordinal)
        {
            ["LCK_M_X"] = new("LCK_M_X", 5000, 50),
            ["WRITELOG"] = new("WRITELOG", 1000, 10),
        };
        var current = new[]
        {
            new SqlPerformanceProbe.WaitRaw("LCK_M_X", 4000, 40),   // рестарт счётчиков (меньше) → пропуск
            new SqlPerformanceProbe.WaitRaw("WRITELOG", 1000, 10),  // без прироста → пропуск
            new SqlPerformanceProbe.WaitRaw("ASYNC_NETWORK_IO", 800, 8), // нет в previous → пропуск
        };

        var deltas = SqlPerformanceProbe.ComputeWaitDeltas(previous, current, top: 10);

        deltas.Should().BeEmpty();
    }

    [Fact]
    public void ComputeWaitDeltas_caps_to_top()
    {
        var previous = new Dictionary<string, SqlPerformanceProbe.WaitRaw>(StringComparer.Ordinal);
        var current = new List<SqlPerformanceProbe.WaitRaw>();
        for (var i = 0; i < 20; i++)
        {
            var type = $"LCK_TYPE_{i:00}";
            previous[type] = new SqlPerformanceProbe.WaitRaw(type, 0, 0);
            current.Add(new SqlPerformanceProbe.WaitRaw(type, (i + 1) * 100, i));
        }

        var deltas = SqlPerformanceProbe.ComputeWaitDeltas(previous, current, top: 5);

        deltas.Should().HaveCount(5);
        deltas[0].WaitTimeMsDelta.Should().Be(2000, "топ-5 по приросту");
    }

    // --- IO-stall дельта ---

    [Fact]
    public void ComputeIoDeltas_returns_growth_ordered_by_total_stall()
    {
        var previous = new Dictionary<int, SqlPerformanceProbe.IoRaw>
        {
            [5] = new(5, "mitpro", Reads: 100, Writes: 50, ReadStallMs: 1000, WriteStallMs: 500),
            [6] = new(6, "test", Reads: 10, Writes: 5, ReadStallMs: 100, WriteStallMs: 50),
        };
        var current = new[]
        {
            new SqlPerformanceProbe.IoRaw(5, "mitpro", 130, 70, 1300, 700),   // +300 read-stall, +200 write-stall
            new SqlPerformanceProbe.IoRaw(6, "test", 20, 10, 4100, 1050),     // +4000 read-stall — больший суммарный
        };

        var deltas = SqlPerformanceProbe.ComputeIoDeltas(previous, current);

        deltas.Should().HaveCount(2);
        deltas[0].DatabaseName.Should().Be("test", "наибольший суммарный stall идёт первым");
        deltas[0].ReadStallMsDelta.Should().Be(4000);
        deltas[1].DatabaseName.Should().Be("mitpro");
        deltas[1].ReadStallMsDelta.Should().Be(300);
        deltas[1].WriteStallMsDelta.Should().Be(200);
        deltas[1].ReadsDelta.Should().Be(30);
        deltas[1].WritesDelta.Should().Be(20);
    }

    [Fact]
    public void ComputeIoDeltas_skips_idle_and_new_and_reset_databases()
    {
        var previous = new Dictionary<int, SqlPerformanceProbe.IoRaw>
        {
            [5] = new(5, "mitpro", 100, 50, 1000, 500), // не изменится → пропуск
            [7] = new(7, "old", 100, 50, 9000, 9000),   // рестарт счётчиков (меньше) → пропуск
        };
        var current = new[]
        {
            new SqlPerformanceProbe.IoRaw(5, "mitpro", 100, 50, 1000, 500),
            new SqlPerformanceProbe.IoRaw(7, "old", 10, 5, 100, 50),
            new SqlPerformanceProbe.IoRaw(9, "fresh", 1, 1, 1, 1), // нет в previous → пропуск
        };

        var deltas = SqlPerformanceProbe.ComputeIoDeltas(previous, current);

        deltas.Should().BeEmpty();
    }

    // --- признак 1С-SQL ---

    [Theory]
    [InlineData("1CV83 Server", true)]
    [InlineData("1cv83 server", true)]
    [InlineData("  1CV83 Server  ", true)]
    [InlineData("Microsoft SQL Server Management Studio", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsOneCProgram_detects_cluster_program_name(string? programName, bool expected)
    {
        SqlPerformanceProbe.IsOneCProgram(programName).Should().Be(expected);
    }
}
