namespace MitLicenseCenter.Web.Endpoints;

// Контракт snapshot'а активных сеансов: Stage 3 — реальные данные из
// IActiveSessionSnapshotStore, заполняемого ReconciliationJob (cold) и
// HotTierPollingService (hot overlay).
public sealed record SessionSnapshotEntry(
    Guid SessionId,
    Guid ClusterInfobaseId,
    Guid TenantId,
    string TenantName,
    string InfobaseName,
    string AppId,
    string UserName,
    string Host,
    bool ConsumesLicense,
    DateTime StartedAt,
    int DurationSeconds);

public sealed record SessionsSnapshotResponse(
    IReadOnlyList<SessionSnapshotEntry> Items,
    DateTime CapturedAt,
    int TookMs,
    string Source);

public sealed record KillSessionRequest(string? Reason);
