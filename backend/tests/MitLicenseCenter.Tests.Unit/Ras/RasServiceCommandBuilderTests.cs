using FluentAssertions;
using MitLicenseCenter.Infrastructure.Ras;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Ras;

// Сборка строк запуска ras.exe-службы и аргументов sc (MLC-159). Точный синтаксис
// сверен с документацией 1С: ras.exe cluster --service --port=<port> <agent>.
public sealed class RasServiceCommandBuilderTests
{
    private const string RasPath = @"C:\Program Files\1cv8\8.5.1.1302\bin\ras.exe";
    private const string Port = "1545";
    private const string Agent = "localhost:1540";

    [Fact]
    public void BuildRasArguments_matches_official_1c_syntax()
    {
        var args = RasServiceCommandBuilder.BuildRasArguments(Port, Agent);

        // ras.exe cluster --service --port=1545 localhost:1540
        args.Should().Equal("cluster", "--service", "--port=1545", "localhost:1540");
    }

    [Fact]
    public void BuildRasCommandLine_quotes_exe_path_with_spaces()
    {
        var cmd = RasServiceCommandBuilder.BuildRasCommandLine(RasPath, Port, Agent);

        cmd.Should().Be("\"C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\ras.exe\" cluster --service --port=1545 localhost:1540");
    }

    [Fact]
    public void BuildBinPathValue_escapes_inner_quotes_for_sc()
    {
        var binPath = RasServiceCommandBuilder.BuildBinPathValue(RasPath, Port, Agent);

        // Внутренние кавычки вокруг пути экранируются \" — иначе sc обрежет значение.
        binPath.Should().StartWith("\\\"C:\\Program Files");
        binPath.Should().Contain("ras.exe\\\" cluster --service --port=1545 localhost:1540");
    }

    [Fact]
    public void BuildScCreateArguments_keeps_sc_significant_spacing_tokens()
    {
        var args = RasServiceCommandBuilder.BuildScCreateArguments("MitLicenseRas", RasPath, Port, Agent);

        // sc требует токены вида «binPath=», «start=», «DisplayName=» отдельными аргументами
        // (значимый синтаксис «ключ= значение»). binPath включает путь + аргументы ras.exe.
        args.Should().ContainInOrder("create", "MitLicenseRas", "binPath=");
        args.Should().Contain("start=").And.Contain("auto");
        args.Should().Contain(a => a.Contains("ras.exe") && a.Contains("--service"));
    }

    [Fact]
    public void BuildScConfigArguments_targets_existing_service()
    {
        var args = RasServiceCommandBuilder.BuildScConfigArguments("Existing", RasPath, Port, Agent);

        args.Should().ContainInOrder("config", "Existing", "binPath=");
        args.Should().NotContain("DisplayName="); // config не меняет отображаемое имя.
    }

    [Fact]
    public void BuildScStartArguments_is_start_name()
    {
        RasServiceCommandBuilder.BuildScStartArguments("Svc").Should().Equal("start", "Svc");
    }

    [Fact]
    public void Previews_are_human_readable_and_secret_free()
    {
        var create = RasServiceCommandBuilder.BuildScCreatePreview("MitLicenseRas", RasPath, Port, Agent);
        var config = RasServiceCommandBuilder.BuildScConfigPreview("MitLicenseRas", RasPath, Port, Agent);
        var start = RasServiceCommandBuilder.BuildScStartPreview("MitLicenseRas");

        create.Should().StartWith("sc create MitLicenseRas binPath= \"\\\"C:\\Program Files");
        create.Should().Contain("--port=1545 localhost:1540").And.Contain("start= auto");
        // Секреты не задаём — obj=/password= в команде отсутствуют (служба слушает loopback).
        create.Should().NotContain("password=").And.NotContain("obj=");
        config.Should().Contain("sc stop MitLicenseRas").And.Contain("sc config MitLicenseRas");
        start.Should().Be("sc start MitLicenseRas");
    }
}
