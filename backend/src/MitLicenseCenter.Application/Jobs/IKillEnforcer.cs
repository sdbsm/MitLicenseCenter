using MitLicenseCenter.Application.Sessions;

namespace MitLicenseCenter.Application.Jobs;

public interface IKillEnforcer
{
    Task EnforceAsync(SnapshotPayload snapshot, CancellationToken ct);
}
