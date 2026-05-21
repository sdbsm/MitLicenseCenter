using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Clusters;
using MitLicenseCenter.Infrastructure.Clusters.Testing;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// Дебаунсинг: когда цепь уже открыта, повторные вызовы (BrokenCircuitException)
// НЕ должны генерировать дублирующий аудит 300. Polly вызывает OnOpened
// ровно один раз на переход Closed→Open; вызовы в открытой цепи не триггерят колбэк.
public sealed class CircuitDebouncingTests
{
    [Fact]
    public async Task Circuit_open_then_multiple_calls_do_not_duplicate_audit_300()
    {
        var (circuitState, auditCapture) = BuildCircuitState(minimumThroughput: 2, breakDurationSeconds: 120);
        var primary = new AlwaysThrowingClusterClient();
        var fallback = new StubRasClusterClient();
        var resilient = new ResilientClusterClient(primary, fallback, circuitState);

        // Открываем цепь (2 отказа = MinimumThroughput).
        for (var i = 0; i < 2; i++)
        {
            await resilient.Invoking(r => r.ListActiveSessionsAsync(default))
                .Should().ThrowAsync<Exception>();
        }

        var openedCountAfterTrip = auditCapture.Entries
            .Count(e => e.Action == AuditActionType.ClusterAdapterCircuitOpened);
        openedCountAfterTrip.Should().Be(1, "один переход Closed→Open = один аудит");

        // Ещё 5 вызовов при открытой цепи — идут в fallback, аудит не пишется.
        for (var i = 0; i < 5; i++)
        {
            var result = await resilient.ListActiveSessionsAsync(default);
            result.Should().BeEmpty();
        }

        auditCapture.Entries
            .Count(e => e.Action == AuditActionType.ClusterAdapterCircuitOpened)
            .Should().Be(1, "повторные вызовы в открытой цепи не пишут дубль аудита 300");
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

        return (new ClusterCircuitState(
            scopeFactory,
            NullLogger<ClusterCircuitState>.Instance,
            minimumThroughput,
            breakDurationSeconds), auditCapture);
    }

    private sealed class AlwaysThrowingClusterClient : IClusterClient
    {
        public Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct)
            => Task.FromException<IReadOnlyList<ClusterSession>>(new InvalidOperationException("simulated failure"));

        public Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct)
            => Task.FromException<KillSessionResult>(new InvalidOperationException("simulated failure"));

        public Task<ClusterPingResult> PingAsync(CancellationToken ct)
            => Task.FromException<ClusterPingResult>(new InvalidOperationException("simulated failure"));
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
