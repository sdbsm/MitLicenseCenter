using FluentAssertions;
using MitLicenseCenter.Application.Performance;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Performance;

// MLC-064: агрегация процессов в семьи — чистая логика без WMI/Process.
public sealed class ProcessFamilyGroupingTests
{
    private static readonly ProcessFamilyMap Map = ProcessFamilyMap.Parse(null);

    [Fact]
    public void Sums_cpu_ram_and_counts_per_family()
    {
        var samples = new[]
        {
            new ProcessSample("rphost", CpuPercent: 10, RamBytes: 1_000),
            new ProcessSample("rphost", CpuPercent: 5, RamBytes: 2_000),
            new ProcessSample("sqlservr", CpuPercent: 20, RamBytes: 9_000),
            new ProcessSample("chrome", CpuPercent: 1, RamBytes: 500),
        };

        var groups = ProcessFamilyGrouping.Group(samples, Map);

        var oneC = groups.Single(g => g.Family == "OneC");
        oneC.CpuPercent.Should().Be(15);
        oneC.RamBytes.Should().Be(3_000);
        oneC.ProcessCount.Should().Be(2);

        groups.Single(g => g.Family == "Mssql").ProcessCount.Should().Be(1);
        groups.Single(g => g.Family == ProcessFamilyMap.OtherFamily).RamBytes.Should().Be(500);
    }

    [Fact]
    public void Ordered_by_cpu_descending()
    {
        var samples = new[]
        {
            new ProcessSample("chrome", CpuPercent: 1, RamBytes: 0),
            new ProcessSample("sqlservr", CpuPercent: 50, RamBytes: 0),
            new ProcessSample("rphost", CpuPercent: 20, RamBytes: 0),
        };

        var groups = ProcessFamilyGrouping.Group(samples, Map);

        groups.Select(g => g.Family).Should().ContainInOrder("Mssql", "OneC", ProcessFamilyMap.OtherFamily);
    }

    [Fact]
    public void Empty_input_yields_empty_groups()
    {
        ProcessFamilyGrouping.Group([], Map).Should().BeEmpty();
    }
}
