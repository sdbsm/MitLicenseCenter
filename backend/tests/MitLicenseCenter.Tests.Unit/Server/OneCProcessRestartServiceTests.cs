using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Server;
using MitLicenseCenter.Infrastructure.Server;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Server;

// Рестарт рабочего процесса 1С (rphost) по Pid (MLC-220, ADR-56). Завершение ОС-процесса
// rphost с авто-подъёмом кластером. Контракт безопасности: whitelist по rac process list →
// guard по имени процесса → kill → верификация исчезновения Pid с таймаутом; идемпотентность.
//
// Тех-гоча трека «Сервер»: NSubstitute не проксирует internal-реализации Infrastructure —
// IClusterClient/ILocalProcessTerminator подменяются РУЧНЫМИ фейками (не моками). TimeProvider —
// тоже ручной фейк (FakeTimeProvider в Directory.Packages.props не подключён, как и в
// WindowsServiceControllerTests): мгновенный полинг без реальных задержек.
public sealed class OneCProcessRestartServiceTests
{
    private const int Pid = 15876;

    // ── Чистая классификация перед kill (guard по имени) ─────────────────────────────────

    [Fact]
    public void Policy_classifies_missing_process_as_restarted_idempotent()
    {
        // Процесс уже ушёл сам (имя null) — рестартить нечего, идемпотентный успех.
        OneCProcessRestartPolicy.ClassifyBeforeKill(null, "rphost")
            .Should().Be(OneCProcessRestartOutcome.Restarted);
    }

    [Fact]
    public void Policy_classifies_non_rphost_as_pid_reused()
    {
        // ОС переназначила Pid другому процессу — завершать чужой процесс запрещено.
        OneCProcessRestartPolicy.ClassifyBeforeKill("chrome", "rphost")
            .Should().Be(OneCProcessRestartOutcome.PidReused);
    }

    [Fact]
    public void Policy_allows_kill_when_name_matches_rphost_case_insensitive()
    {
        // Имя rphost (регистронезависимо) — kill разрешён (null = «решает вызывающий»).
        OneCProcessRestartPolicy.ClassifyBeforeKill("RPHOST", "rphost").Should().BeNull();
    }

    // ── Whitelist: Pid не в rac process list → не убиваем ─────────────────────────────────

    [Fact]
    public async Task Restart_pid_not_in_cluster_returns_NotInCluster_without_kill()
    {
        // rac отдаёт другой Pid — наш не в whitelist → отказ, kill НЕ вызывается.
        var cluster = new FakeClusterClient([ProcessWithPid(999)]);
        var terminator = new FakeProcessTerminator { Name = "rphost" };
        var service = Build(cluster, terminator);

        var result = await service.RestartAsync(Pid, CancellationToken.None);

        result.Outcome.Should().Be(OneCProcessRestartOutcome.NotInCluster);
        terminator.KillCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Restart_empty_cluster_list_returns_NotInCluster_without_kill()
    {
        // rac недоступен/не настроен → пустой список → whitelist не пройден (НЕ убиваем вслепую).
        // Один снимок — пустой список процессов (явный аргумент, чтобы params не съел его как «ноль снимков»).
        var cluster = new FakeClusterClient(snapshots: [Array.Empty<OneCProcessLoad>()]);
        var terminator = new FakeProcessTerminator { Name = "rphost" };
        var service = Build(cluster, terminator);

        var result = await service.RestartAsync(Pid, CancellationToken.None);

        result.Outcome.Should().Be(OneCProcessRestartOutcome.NotInCluster);
        terminator.KillCalls.Should().BeEmpty();
    }

    // ── Guard по имени процесса ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Restart_pid_reused_by_os_returns_PidReused_without_kill()
    {
        // Pid в whitelist rac, но ОС-процесс с этим Pid — НЕ rphost (переиспользован) → 409, НЕ убиваем.
        var cluster = new FakeClusterClient([ProcessWithPid(Pid)]);
        var terminator = new FakeProcessTerminator { Name = "explorer" };
        var service = Build(cluster, terminator);

        var result = await service.RestartAsync(Pid, CancellationToken.None);

        result.Outcome.Should().Be(OneCProcessRestartOutcome.PidReused);
        terminator.KillCalls.Should().BeEmpty();
    }

    // ── Идемпотентность: процесс уже ушёл ────────────────────────────────────────────────

    [Fact]
    public async Task Restart_os_process_already_gone_returns_Restarted_without_kill()
    {
        // Pid в whitelist (снимок чуть устарел), но ОС-процесса уже нет (имя null) → успех, НЕ убиваем.
        var cluster = new FakeClusterClient([ProcessWithPid(Pid)]);
        var terminator = new FakeProcessTerminator { Name = null };
        var service = Build(cluster, terminator);

        var result = await service.RestartAsync(Pid, CancellationToken.None);

        result.Outcome.Should().Be(OneCProcessRestartOutcome.Restarted);
        terminator.KillCalls.Should().BeEmpty();
    }

    // ── Верификация исчезновения Pid ─────────────────────────────────────────────────────

    [Fact]
    public async Task Restart_kills_then_succeeds_when_pid_disappears()
    {
        // 1-й снимок (whitelist) содержит Pid; после kill снимки уже без него → Restarted.
        var cluster = new FakeClusterClient(
            [ProcessWithPid(Pid)],      // whitelist-проверка
            []);                         // верификация: Pid исчез (кластер заменил процесс)
        var terminator = new FakeProcessTerminator { Name = "rphost" };
        var service = Build(cluster, terminator);

        var result = await service.RestartAsync(Pid, CancellationToken.None);

        result.Outcome.Should().Be(OneCProcessRestartOutcome.Restarted);
        result.Pid.Should().Be(Pid);
        terminator.KillCalls.Should().ContainSingle().Which.Should().Be(Pid);
    }

    [Fact]
    public async Task Restart_times_out_when_pid_never_disappears()
    {
        // Pid остаётся в каждом снимке rac → за таймаут не исчез → VerificationTimedOut (409).
        // Ручной TimeProvider: первый GetUtcNow задаёт deadline, второй уже за ним → один опрос
        // после kill, затем таймаут (без реальных задержек).
        var clock = new SteppingTimeProvider(stepSeconds: 60);
        var cluster = new FakeClusterClient([ProcessWithPid(Pid)]); // последний снимок повторяется
        var terminator = new FakeProcessTerminator { Name = "rphost" };
        var service = Build(cluster, terminator, clock);

        var result = await service.RestartAsync(Pid, CancellationToken.None);

        result.Outcome.Should().Be(OneCProcessRestartOutcome.VerificationTimedOut);
        terminator.KillCalls.Should().ContainSingle();
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────

    private static OneCProcessRestartService Build(
        FakeClusterClient cluster,
        FakeProcessTerminator terminator,
        TimeProvider? clock = null) =>
        new(
            cluster,
            terminator,
            clock ?? new SteppingTimeProvider(stepSeconds: 0),
            // Мизерные таймауты — на случай реального ожидания (полинг идёт через clock).
            new OneCProcessRestartOptions
            {
                PollInterval = TimeSpan.FromMilliseconds(1),
                VerificationTimeout = TimeSpan.FromSeconds(30),
            },
            NullLogger<OneCProcessRestartService>.Instance);

    private static OneCProcessLoad ProcessWithPid(int pid) =>
        new(Process: Guid.NewGuid(), Pid: pid, AvailablePerformance: 500, AvgCallTime: 1.0, MemorySize: 1000);

    // Ручной фейк IClusterClient (NSubstitute не проксирует — гоча трека). Отдаёт
    // последовательность снимков ListProcessesAsync; последний снимок повторяется бесконечно
    // (полинг верификации продолжается с тем же состоянием).
    private sealed class FakeClusterClient : IClusterClient
    {
        private readonly Queue<IReadOnlyList<OneCProcessLoad>> _snapshots;

        public FakeClusterClient(params IReadOnlyList<OneCProcessLoad>[] snapshots)
        {
            _snapshots = new Queue<IReadOnlyList<OneCProcessLoad>>(snapshots);
        }

        public Task<IReadOnlyList<OneCProcessLoad>> ListProcessesAsync(CancellationToken ct)
        {
            // Последний снимок повторяется (полинг продолжается с тем же состоянием).
            var snapshot = _snapshots.Count > 1 ? _snapshots.Dequeue() : _snapshots.Peek();
            return Task.FromResult(snapshot);
        }

        // Остальные методы IClusterClient не нужны рестарту процесса.
        public Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<Guid>?> ListLicensedSessionIdsAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct, string? errorMessage = null) =>
            throw new NotSupportedException();

        public Task<ClusterPingResult> PingAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<ClusterInfobaseDiscoveryResult> ListInfobasesAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<OneCSessionLoad>> ListSessionLoadsAsync(CancellationToken ct) =>
            throw new NotSupportedException();
    }

    // Ручной фейк ILocalProcessTerminator: фиксированное имя процесса + запись вызовов Kill.
    private sealed class FakeProcessTerminator : ILocalProcessTerminator
    {
        public string? Name { get; init; }

        public List<int> KillCalls { get; } = [];

        public string? GetProcessName(int pid) => Name;

        public bool Kill(int pid)
        {
            KillCalls.Add(pid);
            return true;
        }
    }

    // Ручной TimeProvider: каждый вызов GetUtcNow прыгает на stepSeconds вперёд. step=0 —
    // время стоит (Pid успеет исчезнуть в первом же опросе). step=60 — мгновенно за дедлайн.
    private sealed class SteppingTimeProvider : TimeProvider
    {
        private readonly TimeSpan _step;
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public SteppingTimeProvider(int stepSeconds) => _step = TimeSpan.FromSeconds(stepSeconds);

        public override DateTimeOffset GetUtcNow()
        {
            var current = _now;
            _now += _step;
            return current;
        }
    }
}
