using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Ras;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Ras;

// Маппинг 4 состояний диагностики и поведение register/update/start (MLC-159, ADR-47).
// Обнаружение (MLC-162) — через фейковые IServiceRegistryReader/IServiceStateReader:
// тест подаёт заранее заготовленный список служб (включая реальный кейс
// «1C:Enterprise 8.5 Remote Server»), реальный реестр/ServiceController не дёргаются.
// Команды-мутации — через FakeScRunner (реальный sc.exe не запускается).
public sealed class ScRasServiceManagerTests
{
    private const string RasPath85 = @"C:\Program Files\1cv8\8.5.1.1302\bin\ras.exe";
    private const string RasPath83 = @"C:\Program Files\1cv8\8.3.23.1865\bin\ras.exe";

    // ── Обнаружение: реальный кейс из дефекта MLC-162 ───────────────────────────────────

    [Fact]
    public async Task Diagnose_finds_real_1c_remote_server_service_by_image_path()
    {
        // Реальная служба с dev-сервера: имя «1C:Enterprise 8.5 Remote Server», ImagePath
        // с ras.exe — раньше энумерация через sc query её не находила (→ ложное
        // NotRegistered). Реестровое обнаружение должно найти её и классифицировать как Ok.
        var registry = new FakeRegistry()
            .Add("LanmanServer", @"C:\Windows\System32\svchost.exe -k netsvcs")
            .Add("1C:Enterprise 8.5 Remote Server",
                $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        var state = new FakeState().SetRunning("1C:Enterprise 8.5 Remote Server", true);

        var d = await NewManager(registry, state).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.Ok);
        d.Service!.ServiceName.Should().Be("1C:Enterprise 8.5 Remote Server");
        d.Service.IsRunning.Should().BeTrue();
        d.Service.PlatformVersion.Should().Be("8.5.1.1302");
        d.Service.Port.Should().Be("1545");
    }

    // ── Диагностика: 4 состояния ──────────────────────────────────────────────────────

    [Fact]
    public async Task Diagnose_NotRegistered_when_no_ras_service()
    {
        var registry = new FakeRegistry()
            .Add("W3SVC", @"C:\Windows\System32\svchost.exe -k iissvcs");

        var d = await NewManager(registry).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.NotRegistered);
        d.Service.Should().BeNull();
        d.TargetReady.Should().BeTrue();
        d.CommandPreview.Should().Contain("sc create").And.Contain("--port=1545");
    }

    [Fact]
    public async Task Diagnose_Stopped_when_service_present_but_not_running()
    {
        var registry = new FakeRegistry()
            .Add("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        var state = new FakeState().SetRunning("MitLicenseRas", false);

        var d = await NewManager(registry, state).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.Stopped);
        d.Service!.ServiceName.Should().Be("MitLicenseRas");
        d.CommandPreview.Should().Be("sc start MitLicenseRas");
    }

    [Fact]
    public async Task Diagnose_Outdated_when_platform_stale()
    {
        // Служба запущена, но на 8.3, а выбранная платформа — 8.5.
        var registry = new FakeRegistry()
            .Add("OldRas", $"\"{RasPath83}\" cluster --service --port=1545 localhost:1540");
        var state = new FakeState().SetRunning("OldRas", true);

        var d = await NewManager(registry, state, platformVersion: "8.5.1.1302").DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.Outdated);
        d.CommandPreview.Should().Contain("sc config OldRas");
    }

    [Fact]
    public async Task Diagnose_Outdated_when_port_differs_from_endpoint()
    {
        // Запущена на актуальной платформе, но порт 1539 ≠ endpoint 1545.
        var registry = new FakeRegistry()
            .Add("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1539 localhost:1540");
        var state = new FakeState().SetRunning("MitLicenseRas", true);

        var d = await NewManager(registry, state).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.Outdated);
    }

    [Fact]
    public async Task Diagnose_Ok_when_running_current_platform_and_port()
    {
        var registry = new FakeRegistry()
            .Add("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        var state = new FakeState().SetRunning("MitLicenseRas", true);

        var d = await NewManager(registry, state).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.Ok);
        d.CommandPreview.Should().BeNull();
    }

    [Fact]
    public async Task Diagnose_NotRegistered_target_not_ready_when_ras_missing()
    {
        var registry = new FakeRegistry(); // нет служб RAS

        // Резолвер не находит ras.exe выбранной версии.
        var d = await NewManager(registry, rasPath: null).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.NotRegistered);
        d.TargetReady.Should().BeFalse();
        d.Target.Should().BeNull();
        d.CommandPreview.Should().BeNull();
        d.Issue.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Diagnose_treats_service_as_stopped_when_state_unavailable()
    {
        // Служба найдена в реестре, но ServiceController не вернул состояние (служба
        // исчезла между чтением реестра и проверкой) → IsRunning=false → Stopped.
        var registry = new FakeRegistry()
            .Add("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        var state = new FakeState(); // состояния нет

        var d = await NewManager(registry, state).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.Stopped);
        d.Service!.IsRunning.Should().BeFalse();
    }

    // ── Порт агента кластера (MLC-194) ──────────────────────────────────────────────────

    [Fact]
    public async Task Diagnose_custom_agent_port_lands_in_target_and_command()
    {
        // Нестандартный порт агента 1С (1541) должен попасть в адрес-цель ras.exe и в
        // команду регистрации — иначе служба RAS соберёт неверный адрес агента.
        var registry = new FakeRegistry(); // службы RAS нет → preview команды create

        var d = await NewManager(registry, agentPort: "1541").DiagnoseAsync(CancellationToken.None);

        d.Target!.AgentAddress.Should().Be("localhost:1541");
        d.CommandPreview.Should().Contain("localhost:1541");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    public async Task Diagnose_blank_or_invalid_agent_port_falls_back_to_1540(string? agentPort)
    {
        var registry = new FakeRegistry();

        var d = await NewManager(registry, agentPort: agentPort).DiagnoseAsync(CancellationToken.None);

        d.Target!.AgentAddress.Should().Be("localhost:1540");
    }

    // ── Операции ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_runs_sc_create_then_start()
    {
        var registry = new FakeRegistry(); // службы RAS нет → register уместен
        var sc = new FakeScRunner();

        var result = await NewManager(registry, sc: sc).RegisterAsync(CancellationToken.None);

        result.State.Should().Be(RasServiceState.Ok);
        result.PlatformVersion.Should().Be("8.5.1.1302");
        result.Port.Should().Be("1545");
        sc.Calls.Should().Contain(c => c.StartsWith("create ", StringComparison.Ordinal));
        sc.Calls.Should().Contain(c => c.StartsWith("start ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Register_throws_when_ras_service_already_exists()
    {
        var registry = new FakeRegistry()
            .Add("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        var state = new FakeState().SetRunning("MitLicenseRas", true);
        var sc = new FakeScRunner();

        var act = () => NewManager(registry, state, sc: sc).RegisterAsync(CancellationToken.None);

        await act.Should().ThrowAsync<RasServiceOperationException>();
        sc.Calls.Should().NotContain(c => c.StartsWith("create ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Register_throws_when_platform_ras_missing()
    {
        var registry = new FakeRegistry();

        var act = () => NewManager(registry, rasPath: null).RegisterAsync(CancellationToken.None);

        await act.Should().ThrowAsync<RasServiceOperationException>();
    }

    [Fact]
    public async Task Update_stops_configures_and_starts_existing_service()
    {
        var registry = new FakeRegistry()
            .Add("OldRas", $"\"{RasPath83}\" cluster --service --port=1545 localhost:1540");
        var state = new FakeState().SetRunning("OldRas", true);
        var sc = new FakeScRunner();

        var result = await NewManager(registry, state, sc: sc, platformVersion: "8.5.1.1302")
            .UpdateAsync(CancellationToken.None);

        result.ServiceName.Should().Be("OldRas");
        result.PlatformVersion.Should().Be("8.5.1.1302");
        sc.Calls.Should().ContainInOrder("stop OldRas", "config OldRas", "start OldRas");
    }

    [Fact]
    public async Task Update_throws_when_no_service_to_update()
    {
        var registry = new FakeRegistry();

        var act = () => NewManager(registry).UpdateAsync(CancellationToken.None);

        await act.Should().ThrowAsync<RasServiceOperationException>();
    }

    [Fact]
    public async Task Start_runs_sc_start_on_discovered_service()
    {
        var registry = new FakeRegistry()
            .Add("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        var state = new FakeState().SetRunning("MitLicenseRas", false);
        var sc = new FakeScRunner();

        var result = await NewManager(registry, state, sc: sc).StartAsync(CancellationToken.None);

        result.ServiceName.Should().Be("MitLicenseRas");
        sc.Calls.Should().Contain("start MitLicenseRas");
    }

    [Fact]
    public async Task Start_treats_already_running_as_success()
    {
        var registry = new FakeRegistry()
            .Add("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        var state = new FakeState().SetRunning("MitLicenseRas", false);
        var sc = new FakeScRunner { StartExitCode = 1056 }; // ERROR_SERVICE_ALREADY_RUNNING — успех.

        var result = await NewManager(registry, state, sc: sc).StartAsync(CancellationToken.None);

        result.State.Should().Be(RasServiceState.Ok);
    }

    [Fact]
    public async Task Start_throws_when_sc_start_fails()
    {
        var registry = new FakeRegistry()
            .Add("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        var state = new FakeState().SetRunning("MitLicenseRas", false);
        var sc = new FakeScRunner { StartExitCode = 5 }; // ERROR_ACCESS_DENIED.

        var act = () => NewManager(registry, state, sc: sc).StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<RasServiceOperationException>();
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────

    private static ScRasServiceManager NewManager(
        FakeRegistry registry,
        FakeState? state = null,
        FakeScRunner? sc = null,
        string platformVersion = "8.5.1.1302",
        string endpoint = "localhost:1545",
        string? agentPort = "1540",
        string? rasPath = RasPath85)
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCRasEndpoint).Returns(endpoint);
        settings.GetString(SettingKey.OneCRasAgentPort).Returns(agentPort);
        settings.GetString(SettingKey.OneCDefaultPlatformVersion).Returns(platformVersion);

        var resolver = new FakeResolver(rasPath);
        return new ScRasServiceManager(
            sc ?? new FakeScRunner(),
            registry,
            state ?? new FakeState(),
            settings,
            resolver,
            NullLogger<ScRasServiceManager>.Instance);
    }

    private sealed class FakeResolver : IRasExePathResolver
    {
        private readonly string? _path;
        public FakeResolver(string? path) => _path = path;
        public string? ResolveForVersion(string platformVersion) => _path;
    }

    // Фейк реестра: список служб (имя → ImagePath), как их вернул бы реальный
    // RegistryServiceReader, но без обращения к реестру.
    private sealed class FakeRegistry : IServiceRegistryReader
    {
        private readonly List<RegisteredService> _services = new();

        public FakeRegistry Add(string name, string imagePath)
        {
            _services.Add(new RegisteredService(name, imagePath));
            return this;
        }

        public IReadOnlyList<RegisteredService> ReadServices() => _services;
    }

    // Фейк ServiceController: состояние по имени службы. Без записи → ReadState=null
    // (служба не найдена / состояние недоступно).
    private sealed class FakeState : IServiceStateReader
    {
        private readonly Dictionary<string, ServiceState> _byName = new(StringComparer.OrdinalIgnoreCase);

        public FakeState SetRunning(string name, bool running)
        {
            _byName[name] = new ServiceState(running, !running, name);
            return this;
        }

        public ServiceState? ReadState(string serviceName)
            => _byName.TryGetValue(serviceName, out var s) ? s : null;
    }

    // Фейк sc.exe: получает raw-командную строку (как ScProcessRunner кладёт в
    // ProcessStartInfo.Arguments), берёт по первым двум токенам «<sub> <name>» для
    // проверки порядка вызовов команд-мутаций (create/config/start/stop).
    private sealed class FakeScRunner : IScProcessRunner
    {
        public List<string> Calls { get; } = new();
        public int StartExitCode { get; set; }

        public Task<ScResult> RunAsync(string arguments, CancellationToken ct)
        {
            var tokens = arguments.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            var sub = tokens.Length > 0 ? tokens[0] : "";
            var name = tokens.Length > 1 ? tokens[1] : "";
            switch (sub)
            {
                case "start":
                    Calls.Add($"start {name}");
                    return Task.FromResult(new ScResult(StartExitCode, "", ""));

                case "stop":
                    Calls.Add($"stop {name}");
                    return Ok("");

                case "create":
                    Calls.Add($"create {name}");
                    return Ok("[SC] CreateService SUCCESS");

                case "config":
                    Calls.Add($"config {name}");
                    return Ok("[SC] ChangeServiceConfig SUCCESS");

                default:
                    return Ok("");
            }
        }

        private static Task<ScResult> Ok(string stdout) => Task.FromResult(new ScResult(0, stdout, ""));
    }
}
