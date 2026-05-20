using MitLicenseCenter.Application.Sessions;

namespace MitLicenseCenter.Infrastructure.Jobs;

internal sealed class ActiveSessionSnapshotStore : IActiveSessionSnapshotStore
{
    private readonly object _gate = new();
    private SnapshotPayload _current = new([], DateTime.MinValue, 0, "None");

    public void Replace(SnapshotPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        lock (_gate)
        {
            _current = payload;
        }
    }

    public SnapshotPayload Current()
    {
        lock (_gate)
        {
            return _current;
        }
    }
}
