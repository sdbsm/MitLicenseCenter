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
    LicenseStatus LicenseStatus,
    DateTime StartedAtUtc);

// LicenseFactAvailable (ADR-48, MLC-166): был ли факт `rac --licenses` доступен в
// цикле, построившем снимок. false ⇒ панель показывает «данные о лицензиях
// недоступны», а enforcement приостанавливается (KillEnforcer ранний выход).
public sealed record SnapshotPayload(
    IReadOnlyList<SnapshotSessionEntry> Items,
    DateTime CapturedAtUtc,
    int TookMs,
    string Source,
    bool LicenseFactAvailable);

public interface IActiveSessionSnapshotStore
{
    void Replace(SnapshotPayload payload);
    SnapshotPayload Current();
}
