using FluentAssertions;
using MitLicenseCenter.Infrastructure.Ras;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Ras;

// Парсер строки запуска службы RAS (ImagePath из реестра: путь к ras.exe + аргументы).
// Обнаружение через реестр (MLC-162) — парсеры работают на строке пути, источник её
// сменился с sc qc на ImagePath, логика та же.
public sealed class RasImagePathParserTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\ras.exe\" cluster", true)]
    [InlineData("C:\\1cv8\\8.5.1.1302\\bin\\RAS.EXE cluster --service", true)]
    [InlineData("C:\\Windows\\System32\\svchost.exe -k netsvcs", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ReferencesRas_matches_only_ras(string? imagePath, bool expected)
    {
        RasImagePathParser.ReferencesRas(imagePath).Should().Be(expected);
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\ras.exe\" cluster", "8.5.1.1302")]
    [InlineData("C:/Program Files/1cv8/8.3.23.1865/bin/ras.exe cluster", "8.3.23.1865")]
    [InlineData("C:\\custom\\ras.exe cluster", null)]
    [InlineData(null, null)]
    public void ParsePlatformVersion_extracts_from_1cv8_segment(string? imagePath, string? expected)
    {
        RasImagePathParser.ParsePlatformVersion(imagePath).Should().Be(expected);
    }

    [Theory]
    [InlineData("ras.exe cluster --service --port=1545 localhost:1540", "1545")]
    [InlineData("ras.exe cluster --service --port 1539 host:1540", "1539")]
    [InlineData("ras.exe cluster --service localhost:1540", null)]
    [InlineData(null, null)]
    public void ParsePort_reads_port_flag(string? imagePath, string? expected)
    {
        RasImagePathParser.ParsePort(imagePath).Should().Be(expected);
    }
}
