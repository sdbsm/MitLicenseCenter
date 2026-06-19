using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Server;
using MitLicenseCenter.Tests.Unit.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-218 (ADR-55): тело ночной джобы авто-рестарта сервера 1С. Проверяем три инварианта:
// (1) расписание включено → рестартим КАЖДУЮ запущенную службу ragent + аудит срабатывания
//     (код 803, initiator "system") + отметка «прошлого прогона»;
// (2) расписание выключено → no-op (защита от рассинхрона: задание могло остаться в сторадже);
// (3) остановленные ragent не трогаем (профилактикой намеренно остановленный сервер не поднимаем).
// Фейки ручные: IOneCServerDiscovery / IWindowsServiceController / ISettingsStore — internal-
// сервисы Infrastructure NSubstitute не проксирует (нет InternalsVisibleTo для DynamicProxy),
// плюс это даёт точный контроль состояния настроек без БД/TimeProvider-пакета (гоча MLC-212).
public sealed class OneCAutoRestartJobTests
{
    private static readonly DateTime Now = new(2026, 6, 19, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Enabled_restarts_every_running_ragent_and_audits()
    {
        var discovery = new FakeDiscovery(
            new OneCServerStatus("ragent_83", Running: true, PlatformVersion: "8.3.23.1865"),
            new OneCServerStatus("ragent_85", Running: true, PlatformVersion: "8.5.1.1302"));
        var controller = new FakeController();
        var store = new FakeSettingsStore { Enabled = 1 };
        var audit = new TestHelpers.CapturingAuditLogger();

        await NewJob(discovery, controller, store, audit).RunAsync(CancellationToken.None);

        // Рестартнуты обе запущенные службы.
        controller.Restarted.Should().BeEquivalentTo("ragent_83", "ragent_85");

        // Аудит срабатывания: код 803, initiator "system", обе службы в описании, server-scope.
        audit.Entries.Should().ContainSingle();
        var entry = audit.Entries[0];
        entry.Action.Should().Be(AuditActionType.OneCServerAutoRestarted);
        entry.Initiator.Should().Be("system");
        entry.TenantId.Should().BeNull();
        entry.Description.Should().Contain("ragent_83").And.Contain("ragent_85");

        // Отметка «прошлого прогона» записана (UTC round-trip).
        store.Values.Should().ContainKey(SettingKey.OneCAutoRestartLastRunUtc);
        store.Values[SettingKey.OneCAutoRestartLastRunUtc].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Disabled_is_a_noop_no_restart_no_audit_no_mark()
    {
        var discovery = new FakeDiscovery(
            new OneCServerStatus("ragent_83", Running: true, PlatformVersion: null));
        var controller = new FakeController();
        var store = new FakeSettingsStore { Enabled = 0 };
        var audit = new TestHelpers.CapturingAuditLogger();

        await NewJob(discovery, controller, store, audit).RunAsync(CancellationToken.None);

        controller.Restarted.Should().BeEmpty("выключенное расписание — джоба не рестартит (защита от рассинхрона)");
        audit.Entries.Should().BeEmpty("no-op не пишет аудит");
        store.Values.Should().NotContainKey(SettingKey.OneCAutoRestartLastRunUtc, "no-op не отмечает прогон");
    }

    [Fact]
    public async Task Enabled_ignores_stopped_ragent_services()
    {
        var discovery = new FakeDiscovery(
            new OneCServerStatus("ragent_running", Running: true, PlatformVersion: null),
            new OneCServerStatus("ragent_stopped", Running: false, PlatformVersion: null));
        var controller = new FakeController();
        var store = new FakeSettingsStore { Enabled = 1 };
        var audit = new TestHelpers.CapturingAuditLogger();

        await NewJob(discovery, controller, store, audit).RunAsync(CancellationToken.None);

        controller.Restarted.Should().ContainSingle().Which.Should().Be("ragent_running");
        controller.Restarted.Should().NotContain("ragent_stopped");
    }

    [Fact]
    public async Task Enabled_but_no_running_servers_marks_run_without_audit()
    {
        var discovery = new FakeDiscovery(
            new OneCServerStatus("ragent_stopped", Running: false, PlatformVersion: null));
        var controller = new FakeController();
        var store = new FakeSettingsStore { Enabled = 1 };
        var audit = new TestHelpers.CapturingAuditLogger();

        await NewJob(discovery, controller, store, audit).RunAsync(CancellationToken.None);

        controller.Restarted.Should().BeEmpty();
        audit.Entries.Should().BeEmpty("рестартить было нечего — записи о срабатывании нет");
        // Прогон всё равно состоялся (расписание сработало) → отметка времени есть.
        store.Values.Should().ContainKey(SettingKey.OneCAutoRestartLastRunUtc);
    }

    private static OneCAutoRestartJob NewJob(
        FakeDiscovery discovery,
        FakeController controller,
        FakeSettingsStore store,
        TestHelpers.CapturingAuditLogger audit) =>
        new(
            discovery,
            controller,
            store,
            audit,
            TestHelpers.FixedClock(Now),
            NullLogger<OneCAutoRestartJob>.Instance);

    // ── Фейки ───────────────────────────────────────────────────────────────────────────

    private sealed class FakeDiscovery : IOneCServerDiscovery
    {
        private readonly IReadOnlyList<OneCServerStatus> _servers;
        public FakeDiscovery(params OneCServerStatus[] servers) => _servers = servers;
        public IReadOnlyList<OneCServerStatus> Discover() => _servers;
    }

    private sealed class FakeController : IWindowsServiceController
    {
        public List<string> Restarted { get; } = [];

        public Task<WindowsServiceOperationResult> RestartAsync(string serviceName, CancellationToken ct)
        {
            Restarted.Add(serviceName);
            return Task.FromResult(new WindowsServiceOperationResult(serviceName, WindowsServiceStatus.Running));
        }

        public Task<WindowsServiceOperationResult> StartAsync(string serviceName, CancellationToken ct) =>
            Task.FromResult(new WindowsServiceOperationResult(serviceName, WindowsServiceStatus.Running));

        public Task<WindowsServiceOperationResult> StopAsync(string serviceName, CancellationToken ct) =>
            Task.FromResult(new WindowsServiceOperationResult(serviceName, WindowsServiceStatus.Stopped));
    }

    // Минимальный in-memory ISettingsStore: enabled — int-настройка, остальное — строки.
    private sealed class FakeSettingsStore : ISettingsStore
    {
        public int? Enabled { get; set; }
        public Dictionary<string, string?> Values { get; } = new(StringComparer.Ordinal);

        public Task<int?> GetIntAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(key == SettingKey.OneCAutoRestartEnabled ? Enabled : null);

        public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(Values.TryGetValue(key, out var v) ? v : null);

        public Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyDictionary<string, string?>)Values);

        public Task SetAsync(string key, string? value, bool isSecret, string updatedBy, CancellationToken ct = default)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SettingDescriptor>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult((IReadOnlyList<SettingDescriptor>)[]);
    }
}
