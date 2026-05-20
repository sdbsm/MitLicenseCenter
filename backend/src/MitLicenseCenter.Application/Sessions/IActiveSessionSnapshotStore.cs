namespace MitLicenseCenter.Application.Sessions;

public sealed record SnapshotSessionEntry(
    Guid SessionId,
    Guid ClusterInfobaseId,
    Guid TenantId,
    string TenantName,
    string InfobaseName,
    string AppId,
    string UserName,
    string Host,
    bool ConsumesLicense,
    DateTime StartedAtUtc);

public sealed record SnapshotPayload(
    IReadOnlyList<SnapshotSessionEntry> Items,
    DateTime CapturedAtUtc,
    int TookMs,
    string Source);

public interface IActiveSessionSnapshotStore
{
    void Replace(SnapshotPayload payload);
    SnapshotPayload Current();
}
