namespace MitLicenseCenter.Application.Clusters;

public interface IClusterClient
{
    Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct);
    Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct);
    Task<ClusterPingResult> PingAsync(CancellationToken ct);

    // Discovery: перечень инфобаз, зарегистрированных в кластере 1С.
    // Используется формами вместо ручного ввода UUID инфобазы. Работает с теми
    // же cluster-admin кредами, что и остальные команды (BuildArgsWithAuth).
    Task<ClusterInfobaseDiscoveryResult> ListInfobasesAsync(CancellationToken ct);
}
