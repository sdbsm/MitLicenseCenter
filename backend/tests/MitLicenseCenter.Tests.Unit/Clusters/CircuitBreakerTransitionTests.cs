using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Clusters;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// Проверяем: после N отказов primary-клиента цепь размыкается (аудит 300 один раз);
// последующие вызовы идут через fallback и возвращают его данные.
public sealed class CircuitBreakerTransitionTests
{
    [Fact]
    public async Task After_MinimumThroughput_failures_circuit_opens_and_audit_300_written_once()
    {
        // Arrange — circuit открывается после 3 отказов.
        var (circuitState, auditCapture) = BuildCircuitState(minimumThroughput: 3, breakDurationSeconds: 60);
        var primary = new AlwaysThrowingClusterClient();
        var fallback = new StubRasClusterClient();
        var resilient = new ResilientClusterClient(primary, fallback, circuitState);

        // Act — 3 вызова (каждый кидает исключение, регистрируемое circuit breaker'ом).
        for (var i = 0; i < 3; i++)
        {
            await resilient.Invoking(r => r.ListActiveSessionsAsync(default))
                .Should().ThrowAsync<Exception>();
        }

        // 4-й вызов: цепь уже открыта → BrokenCircuitException → fallback возвращает пустой список.
        var result = await resilient.ListActiveSessionsAsync(default);

        // Assert
        result.Should().BeEmpty("fallback (StubRasClusterClient) возвращает пустой список");
        auditCapture.Entries
            .Where(e => e.Action == AuditActionType.ClusterAdapterCircuitOpened)
            .Should().ContainSingle("OnOpened срабатывает ровно один раз при размыкании");
    }

    [Fact]
    public async Task After_circuit_opens_and_probe_succeeds_circuit_closes_and_audit_301_written()
    {
        // Arrange — очень короткий BreakDuration чтобы не ждать в тесте.
        var (circuitState, auditCapture) = BuildCircuitState(minimumThroughput: 2, breakDurationSeconds: 1);
        var primary = new RecoveringClusterClient(failCount: 2);
        var fallback = new StubRasClusterClient();
        var resilient = new ResilientClusterClient(primary, fallback, circuitState);

        // Открываем цепь (2 отказа).
        for (var i = 0; i < 2; i++)
        {
            await resilient.Invoking(r => r.PingAsync(default)).Should().ThrowAsync<Exception>();
        }

        auditCapture.Entries
            .Should().ContainSingle(e => e.Action == AuditActionType.ClusterAdapterCircuitOpened);

        // Ждём BreakDuration (1 сек) чтобы цепь перешла в HalfOpen.
        await Task.Delay(TimeSpan.FromSeconds(1.2));

        // Probe: primary теперь успешен → цепь замыкается.
        var ping = await resilient.PingAsync(default);

        // Assert
        ping.Ok.Should().BeTrue("primary успешен после probe");
        auditCapture.Entries
            .Where(e => e.Action == AuditActionType.ClusterAdapterCircuitClosed)
            .Should().ContainSingle("OnClosed срабатывает ровно один раз при замыкании");
    }

    // ---

    private static (ClusterCircuitState, CapturingAuditLogger) BuildCircuitState(
        int minimumThroughput,
        int breakDurationSeconds)
    {
        var auditCapture = new CapturingAuditLogger();

        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IAuditLogger)).Returns(auditCapture);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(sp);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var state = new ClusterCircuitState(
            scopeFactory,
            NullLogger<ClusterCircuitState>.Instance,
            minimumThroughput,
            breakDurationSeconds);

        return (state, auditCapture);
    }

    // --- Test doubles ---

    private sealed class AlwaysThrowingClusterClient : IClusterClient
    {
        public Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct)
            => Task.FromException<IReadOnlyList<ClusterSession>>(new InvalidOperationException("simulated REST failure"));

        public Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct)
            => Task.FromException<KillSessionResult>(new InvalidOperationException("simulated REST failure"));

        public Task<ClusterPingResult> PingAsync(CancellationToken ct)
            => Task.FromException<ClusterPingResult>(new InvalidOperationException("simulated REST failure"));
    }

    // Первые `failCount` вызовов кидают исключение; дальше — Ok.
    private sealed class RecoveringClusterClient(int failCount) : IClusterClient
    {
        private int _callCount;

        public Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref _callCount) <= failCount)
            {
                return Task.FromException<IReadOnlyList<ClusterSession>>(new InvalidOperationException("simulated failure"));
            }
            return Task.FromResult<IReadOnlyList<ClusterSession>>(Array.Empty<ClusterSession>());
        }

        public Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct)
            => Task.FromResult(new KillSessionResult(true, false));

        public Task<ClusterPingResult> PingAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref _callCount) <= failCount)
            {
                return Task.FromException<ClusterPingResult>(new InvalidOperationException("simulated failure"));
            }
            return Task.FromResult(new ClusterPingResult(true, null));
        }
    }

    private sealed class CapturingAuditLogger : IAuditLogger
    {
        public List<(AuditActionType Action, string Initiator)> Entries { get; } = [];

        public Task LogAsync(
            AuditActionType action,
            string initiator,
            string description,
            Guid? tenantId = null,
            AuditReason? reason = null,
            CancellationToken ct = default)
        {
            Entries.Add((action, initiator));
            return Task.CompletedTask;
        }
    }
}
