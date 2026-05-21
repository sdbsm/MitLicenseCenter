using MitLicenseCenter.Application.Clusters;

namespace MitLicenseCenter.Infrastructure.Clusters.Testing;

// Заглушка RAS-адаптера, доступная unit-тестам PR 3.2/3.3 (CircuitBreakerTransitionTests
// и подобные). С PR 3.8 в production-DI не регистрируется — там
// RacExecutableRasClusterClient. Stub оставлен в Testing/ через
// InternalsVisibleTo MitLicenseCenter.Tests.Unit, чтобы существующие тесты
// продолжили компилироваться без переписывания.
//
// Семантика:
//   PingAsync                 → Ok=true (для тестов circuit-cycle'ов с зелёным fallback)
//   ListActiveSessionsAsync   → пустой список (Ras-ветка не ломает snapshot store)
//   KillSessionAsync          → AlreadyGone=true (идемпотентный no-op)
internal sealed class StubRasClusterClient : IRasFallbackClusterClient
{
    public Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ClusterSession>>(Array.Empty<ClusterSession>());

    public Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct)
        => Task.FromResult(new KillSessionResult(Killed: false, AlreadyGone: true));

    public Task<ClusterPingResult> PingAsync(CancellationToken ct)
        => Task.FromResult(new ClusterPingResult(Ok: true, Error: null));
}
