namespace MitLicenseCenter.Application.Clusters;

public interface IClusterClient
{
    Task<IReadOnlyList<ClusterSession>> ListActiveSessionsAsync(CancellationToken ct);
    Task<KillSessionResult> KillSessionAsync(SessionDescriptor descriptor, CancellationToken ct);
    Task<ClusterPingResult> PingAsync(CancellationToken ct);
}
