using FluentAssertions;
using MitLicenseCenter.Infrastructure.Ras;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Ras;

// Парсер вывода sc query / sc qc (MLC-159). Опираемся на «машинные» поля
// (SERVICE_NAME/STATE/BINARY_PATH_NAME), печатаемые латиницей независимо от языка ОС.
public sealed class ScOutputParserTests
{
    private const string QueryAllSample = """
SERVICE_NAME: W3SVC
DISPLAY_NAME: Служба веб-публикации
        TYPE               : 20  WIN32_SHARE_PROCESS
        STATE              : 4  RUNNING

SERVICE_NAME: MitLicenseRas
DISPLAY_NAME: 1C:Enterprise Remote Administration Server
        TYPE               : 10  WIN32_OWN_PROCESS
        STATE              : 1  STOPPED
""";

    [Fact]
    public void ParseServiceNames_extracts_all_names()
    {
        var names = ScOutputParser.ParseServiceNames(QueryAllSample);

        names.Should().Equal("W3SVC", "MitLicenseRas");
    }

    [Fact]
    public void ParseServiceNames_empty_for_blank()
    {
        ScOutputParser.ParseServiceNames("").Should().BeEmpty();
        ScOutputParser.ParseServiceNames("какой-то мусор без полей").Should().BeEmpty();
    }

    [Theory]
    [InlineData("        STATE              : 4  RUNNING", true)]
    [InlineData("        STATE              : 1  STOPPED", false)]
    [InlineData("        STATE              : 2  START_PENDING", false)]
    [InlineData("        STATE              : 3  STOP_PENDING", false)]
    [InlineData("нет состояния", false)]
    public void ParseIsRunning_reads_state_code(string queryOutput, bool expected)
    {
        ScOutputParser.ParseIsRunning(queryOutput).Should().Be(expected);
    }

    [Fact]
    public void ParseBinaryPath_reads_quoted_path_with_args()
    {
        const string qc = """
[SC] QueryServiceConfig SUCCESS

SERVICE_NAME: MitLicenseRas
        TYPE               : 10  WIN32_OWN_PROCESS
        START_TYPE         : 2   AUTO_START
        BINARY_PATH_NAME   : "C:\Program Files\1cv8\8.5.1.1302\bin\ras.exe" cluster --service --port=1545 localhost:1540
        DISPLAY_NAME       : 1C:Enterprise Remote Administration Server
""";

        var binPath = ScOutputParser.ParseBinaryPath(qc);

        binPath.Should().Be("\"C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\ras.exe\" cluster --service --port=1545 localhost:1540");
    }

    [Fact]
    public void ParseBinaryPath_null_when_absent()
    {
        ScOutputParser.ParseBinaryPath("SERVICE_NAME: X\n").Should().BeNull();
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\ras.exe\" cluster", true)]
    [InlineData("C:\\1cv8\\8.5.1.1302\\bin\\RAS.EXE cluster --service", true)]
    [InlineData("C:\\Windows\\System32\\svchost.exe -k netsvcs", false)]
    [InlineData(null, false)]
    public void BinPathReferencesRas_matches_only_ras(string? binPath, bool expected)
    {
        ScOutputParser.BinPathReferencesRas(binPath).Should().Be(expected);
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\ras.exe\" cluster", "8.5.1.1302")]
    [InlineData("C:/Program Files/1cv8/8.3.23.1865/bin/ras.exe cluster", "8.3.23.1865")]
    [InlineData("C:\\custom\\ras.exe cluster", null)]
    [InlineData(null, null)]
    public void ParsePlatformVersion_extracts_from_1cv8_segment(string? binPath, string? expected)
    {
        ScOutputParser.ParsePlatformVersion(binPath).Should().Be(expected);
    }

    [Theory]
    [InlineData("ras.exe cluster --service --port=1545 localhost:1540", "1545")]
    [InlineData("ras.exe cluster --service --port 1539 host:1540", "1539")]
    [InlineData("ras.exe cluster --service localhost:1540", null)]
    [InlineData(null, null)]
    public void ParsePort_reads_port_flag(string? binPath, string? expected)
    {
        ScOutputParser.ParsePort(binPath).Should().Be(expected);
    }
}
