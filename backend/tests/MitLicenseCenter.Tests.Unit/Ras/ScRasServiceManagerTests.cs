using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Ras;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Ras;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Ras;

// Маппинг 4 состояний диагностики и поведение register/update/start через мок sc-раннера
// (MLC-159, ADR-47). Реальный sc.exe не запускается — FakeScRunner отвечает по sub-команде.
public sealed class ScRasServiceManagerTests
{
    private const string RasPath85 = @"C:\Program Files\1cv8\8.5.1.1302\bin\ras.exe";
    private const string RasPath83 = @"C:\Program Files\1cv8\8.3.23.1865\bin\ras.exe";

    // ── Диагностика: 4 состояния ──────────────────────────────────────────────────────

    [Fact]
    public async Task Diagnose_NotRegistered_when_no_ras_service()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames("W3SVC");
        sc.SetQc("W3SVC", @"C:\Windows\System32\svchost.exe -k iissvcs");

        var d = await NewManager(sc).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.NotRegistered);
        d.Service.Should().BeNull();
        d.TargetReady.Should().BeTrue();
        d.CommandPreview.Should().Contain("sc create").And.Contain("--port=1545");
    }

    [Fact]
    public async Task Diagnose_Stopped_when_service_present_but_not_running()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames("MitLicenseRas");
        sc.SetQc("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        sc.SetQueryState("MitLicenseRas", running: false);

        var d = await NewManager(sc).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.Stopped);
        d.Service!.ServiceName.Should().Be("MitLicenseRas");
        d.CommandPreview.Should().Be("sc start MitLicenseRas");
    }

    [Fact]
    public async Task Diagnose_Outdated_when_platform_stale()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames("OldRas");
        // Служба запущена, но на 8.3, а выбранная платформа — 8.5.
        sc.SetQc("OldRas", $"\"{RasPath83}\" cluster --service --port=1545 localhost:1540");
        sc.SetQueryState("OldRas", running: true);

        var d = await NewManager(sc, platformVersion: "8.5.1.1302").DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.Outdated);
        d.CommandPreview.Should().Contain("sc config OldRas");
    }

    [Fact]
    public async Task Diagnose_Outdated_when_port_differs_from_endpoint()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames("MitLicenseRas");
        // Запущена на актуальной платформе, но порт 1539 ≠ endpoint 1545.
        sc.SetQc("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1539 localhost:1540");
        sc.SetQueryState("MitLicenseRas", running: true);

        var d = await NewManager(sc).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.Outdated);
    }

    [Fact]
    public async Task Diagnose_Ok_when_running_current_platform_and_port()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames("MitLicenseRas");
        sc.SetQc("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        sc.SetQueryState("MitLicenseRas", running: true);

        var d = await NewManager(sc).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.Ok);
        d.CommandPreview.Should().BeNull();
    }

    [Fact]
    public async Task Diagnose_NotRegistered_target_not_ready_when_ras_missing()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames(); // нет служб RAS

        // Резолвер не находит ras.exe выбранной версии.
        var d = await NewManager(sc, rasPath: null).DiagnoseAsync(CancellationToken.None);

        d.State.Should().Be(RasServiceState.NotRegistered);
        d.TargetReady.Should().BeFalse();
        d.Target.Should().BeNull();
        d.CommandPreview.Should().BeNull();
        d.Issue.Should().NotBeNullOrEmpty();
    }

    // ── Операции ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_runs_sc_create_then_start()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames(); // службы RAS нет → register уместен

        var result = await NewManager(sc).RegisterAsync(CancellationToken.None);

        result.State.Should().Be(RasServiceState.Ok);
        result.PlatformVersion.Should().Be("8.5.1.1302");
        result.Port.Should().Be("1545");
        sc.Calls.Should().Contain(c => c.StartsWith("create ", StringComparison.Ordinal));
        sc.Calls.Should().Contain(c => c.StartsWith("start ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Register_throws_when_ras_service_already_exists()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames("MitLicenseRas");
        sc.SetQc("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        sc.SetQueryState("MitLicenseRas", running: true);

        var act = () => NewManager(sc).RegisterAsync(CancellationToken.None);

        await act.Should().ThrowAsync<RasServiceOperationException>();
        sc.Calls.Should().NotContain(c => c.StartsWith("create ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Register_throws_when_platform_ras_missing()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames();

        var act = () => NewManager(sc, rasPath: null).RegisterAsync(CancellationToken.None);

        await act.Should().ThrowAsync<RasServiceOperationException>();
    }

    [Fact]
    public async Task Update_stops_configures_and_starts_existing_service()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames("OldRas");
        sc.SetQc("OldRas", $"\"{RasPath83}\" cluster --service --port=1545 localhost:1540");
        sc.SetQueryState("OldRas", running: true);

        var result = await NewManager(sc, platformVersion: "8.5.1.1302").UpdateAsync(CancellationToken.None);

        result.ServiceName.Should().Be("OldRas");
        result.PlatformVersion.Should().Be("8.5.1.1302");
        sc.Calls.Should().ContainInOrder("stop OldRas", "config OldRas", "start OldRas");
    }

    [Fact]
    public async Task Update_throws_when_no_service_to_update()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames();

        var act = () => NewManager(sc).UpdateAsync(CancellationToken.None);

        await act.Should().ThrowAsync<RasServiceOperationException>();
    }

    [Fact]
    public async Task Start_runs_sc_start_on_discovered_service()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames("MitLicenseRas");
        sc.SetQc("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        sc.SetQueryState("MitLicenseRas", running: false);

        var result = await NewManager(sc).StartAsync(CancellationToken.None);

        result.ServiceName.Should().Be("MitLicenseRas");
        sc.Calls.Should().Contain("start MitLicenseRas");
    }

    [Fact]
    public async Task Start_treats_already_running_as_success()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames("MitLicenseRas");
        sc.SetQc("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        sc.SetQueryState("MitLicenseRas", running: false);
        sc.StartExitCode = 1056; // ERROR_SERVICE_ALREADY_RUNNING — успех.

        var result = await NewManager(sc).StartAsync(CancellationToken.None);

        result.State.Should().Be(RasServiceState.Ok);
    }

    [Fact]
    public async Task Start_throws_when_sc_start_fails()
    {
        var sc = new FakeScRunner();
        sc.QueryAllNames("MitLicenseRas");
        sc.SetQc("MitLicenseRas", $"\"{RasPath85}\" cluster --service --port=1545 localhost:1540");
        sc.SetQueryState("MitLicenseRas", running: false);
        sc.StartExitCode = 5; // ERROR_ACCESS_DENIED.

        var act = () => NewManager(sc).StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<RasServiceOperationException>();
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────

    private static ScRasServiceManager NewManager(
        FakeScRunner sc,
        string platformVersion = "8.5.1.1302",
        string endpoint = "localhost:1545",
        string? rasPath = RasPath85)
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCRasEndpoint).Returns(endpoint);
        settings.GetString(SettingKey.OneCDefaultPlatformVersion).Returns(platformVersion);

        var resolver = new FakeResolver(rasPath);
        return new ScRasServiceManager(sc, settings, resolver, NullLogger<ScRasServiceManager>.Instance);
    }

    private sealed class FakeResolver : IRasExePathResolver
    {
        private readonly string? _path;
        public FakeResolver(string? path) => _path = path;
        public string? ResolveForVersion(string platformVersion) => _path;
    }

    // Фейк sc.exe: отвечает по первому аргументу (query/qc/create/config/start/stop).
    // Records flat «<sub> <name>» строки для проверки порядка вызовов.
    private sealed class FakeScRunner : IScProcessRunner
    {
        private readonly Dictionary<string, string> _qcByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _runningByName = new(StringComparer.OrdinalIgnoreCase);
        private string _queryAll = "";

        public List<string> Calls { get; } = new();
        public int StartExitCode { get; set; }

        public void QueryAllNames(params string[] names)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var n in names)
            {
                sb.Append("SERVICE_NAME: ").Append(n).Append('\n');
                sb.Append("        STATE              : 1  STOPPED\n\n");
            }
            _queryAll = sb.ToString();
        }

        public void SetQc(string name, string binPath)
            => _qcByName[name] = $"BINARY_PATH_NAME   : {binPath}\n";

        public void SetQueryState(string name, bool running)
            => _runningByName[name] = running;

        public Task<ScResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken ct)
        {
            var sub = arguments[0];
            switch (sub)
            {
                case "query" when arguments.Count >= 2 && arguments[1] == "state=":
                    return Ok(_queryAll);

                case "query":
                    {
                        var name = arguments[1];
                        var running = _runningByName.TryGetValue(name, out var r) && r;
                        var state = running ? "4  RUNNING" : "1  STOPPED";
                        return Ok($"SERVICE_NAME: {name}\n        STATE              : {state}\n");
                    }

                case "qc":
                    {
                        var name = arguments[1];
                        return _qcByName.TryGetValue(name, out var qc)
                            ? Ok(qc)
                            : Task.FromResult(new ScResult(1060, "", "не найдена"));
                    }

                case "start":
                    Calls.Add($"start {arguments[1]}");
                    return Task.FromResult(new ScResult(StartExitCode, "", ""));

                case "stop":
                    Calls.Add($"stop {arguments[1]}");
                    return Ok("");

                case "create":
                    Calls.Add($"create {arguments[1]}");
                    return Ok("[SC] CreateService SUCCESS");

                case "config":
                    Calls.Add($"config {arguments[1]}");
                    return Ok("[SC] ChangeServiceConfig SUCCESS");

                default:
                    return Ok("");
            }
        }

        private static Task<ScResult> Ok(string stdout) => Task.FromResult(new ScResult(0, stdout, ""));
    }
}
