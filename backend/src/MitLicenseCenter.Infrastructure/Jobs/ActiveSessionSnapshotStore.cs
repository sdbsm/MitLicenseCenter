using MitLicenseCenter.Application.Sessions;

namespace MitLicenseCenter.Infrastructure.Jobs;

internal sealed class ActiveSessionSnapshotStore : IActiveSessionSnapshotStore
{
    private readonly object _gate = new();
    // Стартовый снимок до первого холодного цикла: факт лицензий ещё недоступен.
    private SnapshotPayload _current = new([], DateTime.MinValue, 0, "None", LicenseFactAvailable: false);

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
