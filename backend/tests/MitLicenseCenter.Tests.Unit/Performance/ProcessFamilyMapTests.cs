using FluentAssertions;
using MitLicenseCenter.Application.Performance;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Performance;

// MLC-064: маппинг «имя процесса → семья» — чистая логика без WMI/Process.
public sealed class ProcessFamilyMapTests
{
    [Theory]
    [InlineData("rphost", "OneC")]
    [InlineData("ragent", "OneC")]
    [InlineData("rmngr", "OneC")]
    [InlineData("sqlservr", "Mssql")]
    [InlineData("TiWorker", "OsUpdate")]
    [InlineData("MsMpEng", "Antivirus")]
    public void Default_map_classifies_known_processes(string process, string expectedFamily)
    {
        var map = ProcessFamilyMap.Parse(null);
        map.Classify(process).Should().Be(expectedFamily);
    }

    [Fact]
    public void Unknown_process_falls_into_Other()
    {
        var map = ProcessFamilyMap.Parse(null);
        map.Classify("chrome").Should().Be(ProcessFamilyMap.OtherFamily);
    }

    [Fact]
    public void Classification_is_case_insensitive()
    {
        var map = ProcessFamilyMap.Parse(null);
        map.Classify("RPHOST").Should().Be("OneC");
        map.Classify("SqlServr").Should().Be("Mssql");
    }

    [Fact]
    public void Blank_falls_back_to_default()
    {
        ProcessFamilyMap.Parse("   ").Classify("rphost").Should().Be("OneC");
        ProcessFamilyMap.Parse("").Classify("sqlservr").Should().Be("Mssql");
    }

    [Fact]
    public void Malformed_string_falls_back_to_default()
    {
        // Ни одной валидной семьи (нет '=') → откат на дефолтный набор.
        var map = ProcessFamilyMap.Parse(";;garbage,no,equals;;");
        map.Classify("rphost").Should().Be("OneC");
    }

    [Fact]
    public void Operator_override_replaces_default()
    {
        var map = ProcessFamilyMap.Parse("Backup=veeam,backupexec; Custom=myapp");

        map.Classify("veeam").Should().Be("Backup");
        map.Classify("myapp").Should().Be("Custom");
        // Дефолтные семьи переопределены целиком — rphost больше не «OneC».
        map.Classify("rphost").Should().Be(ProcessFamilyMap.OtherFamily);
    }

    [Fact]
    public void First_matching_family_wins()
    {
        var map = ProcessFamilyMap.Parse("A=dup;B=dup");
        map.Classify("dup").Should().Be("A");
    }
}
