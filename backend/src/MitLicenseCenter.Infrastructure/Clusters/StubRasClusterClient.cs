using MitLicenseCenter.Application.Clusters;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Заглушка RAS-адаптера до PR 3.8, где будет реализован RacExecutableRasClusterClient.
// PingAsync → успешно (чтобы не тревожить /cluster/status пока нет реального RAS).
// ListActiveSessionsAsync → пустой список (fallback-режим виден оператору через
// SessionsPage, но не ломает snapshot store).
internal sealed class StubRasClusterClient : IRasFallbackClusterClient
{
    public Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ClusterSession>>(Array.Empty<ClusterSession>());

    // AlreadyGone: true — идемпотентный результат: «считаем, что сеанса нет»,
    // enforcer не бросает исключение и пишет аудит как при успешном kill.
    public Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct)
        => Task.FromResult(new KillSessionResult(Killed: false, AlreadyGone: true));

    public Task<ClusterPingResult> PingAsync(CancellationToken ct)
        => Task.FromResult(new ClusterPingResult(Ok: true, Error: null));
}
