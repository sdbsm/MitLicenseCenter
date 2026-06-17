using FluentAssertions;
using MitLicenseCenter.Infrastructure.Ras;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Ras;

// Сборка строк запуска ras.exe-службы и raw-команд sc (MLC-159, точные строки — MLC-162).
// Синтаксис сверен с документацией 1С: ras.exe cluster --service --port=<port> <agent>.
// Команды sc собираются ОДНОЙ raw-строкой (ProcessStartInfo.Arguments) под нестандартный
// парсер sc.exe «ключ= значение» — поэтому проверяем точную командную строку.
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
    public void BuildRasArguments_carries_custom_agent_port()
    {
        // MLC-194: при нестандартном порту агента кластера (1541) адрес-цель ras.exe
        // должен нести этот порт — иначе служба RAS не цепляется к ragent.
        var args = RasServiceCommandBuilder.BuildRasArguments(Port, "localhost:1541");

        args.Should().Equal("cluster", "--service", "--port=1545", "localhost:1541");
    }

    [Fact]
    public void BuildScCreateArguments_carries_custom_agent_port()
    {
        var args = RasServiceCommandBuilder.BuildScCreateArguments("MitLicenseRas", RasPath, Port, "localhost:1541");

        args.Should().Contain("--port=1545 localhost:1541");
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
    public void BuildScCreateArguments_is_exact_raw_command_line()
    {
        var args = RasServiceCommandBuilder.BuildScCreateArguments("MitLicenseRas", RasPath, Port, Agent);

        // Точная строка для ProcessStartInfo.Arguments: значение binPath= обёрнуто внешними
        // кавычками, путь к ras.exe внутри экранирован \"…\", «ключ= значение» с пробелом
        // после '=' (как требует sc.exe и как делает установщик в [Code]).
        args.Should().Be(
            "create MitLicenseRas binPath= \"\\\"C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\ras.exe\\\" " +
            "cluster --service --port=1545 localhost:1540\" start= auto " +
            "DisplayName= \"1C:Enterprise Remote Administration Server (MitLicense)\"");

        // Секреты не задаём — obj=/password= в команде отсутствуют (служба слушает loopback).
        args.Should().NotContain("password=").And.NotContain("obj=");
    }

    [Fact]
    public void BuildScConfigArguments_is_exact_raw_command_line()
    {
        var args = RasServiceCommandBuilder.BuildScConfigArguments("Existing", RasPath, Port, Agent);

        // config выравнивает binPath/порт существующей службы; DisplayName не трогает.
        args.Should().Be(
            "config Existing binPath= \"\\\"C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\ras.exe\\\" " +
            "cluster --service --port=1545 localhost:1540\" start= auto");
        args.Should().NotContain("DisplayName=");
    }

    [Fact]
    public void BuildScStartArguments_is_start_name()
    {
        RasServiceCommandBuilder.BuildScStartArguments("Svc").Should().Be("start Svc");
    }

    [Fact]
    public void BuildScStopArguments_is_stop_name()
    {
        RasServiceCommandBuilder.BuildScStopArguments("Svc").Should().Be("stop Svc");
    }

    [Fact]
    public void Preview_matches_executed_create_command()
    {
        var create = RasServiceCommandBuilder.BuildScCreatePreview("MitLicenseRas", RasPath, Port, Agent);
        var executed = RasServiceCommandBuilder.BuildScCreateArguments("MitLicenseRas", RasPath, Port, Agent);

        // Предпросмотр = «sc » + исполняемая команда: оператор видит ровно то, что выполнит панель.
        create.Should().Be("sc " + executed);
    }

    [Fact]
    public void Previews_are_human_readable_and_secret_free()
    {
        var create = RasServiceCommandBuilder.BuildScCreatePreview("MitLicenseRas", RasPath, Port, Agent);
        var config = RasServiceCommandBuilder.BuildScConfigPreview("MitLicenseRas", RasPath, Port, Agent);
        var start = RasServiceCommandBuilder.BuildScStartPreview("MitLicenseRas");

        create.Should().StartWith("sc create MitLicenseRas binPath= \"\\\"C:\\Program Files");
        create.Should().Contain("--port=1545 localhost:1540").And.Contain("start= auto");
        create.Should().NotContain("password=").And.NotContain("obj=");
        config.Should().Contain("sc stop MitLicenseRas").And.Contain("sc config MitLicenseRas");
        start.Should().Be("sc start MitLicenseRas");
    }
}
