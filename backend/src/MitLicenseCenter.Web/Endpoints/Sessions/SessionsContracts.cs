using MitLicenseCenter.Application.Sessions;

namespace MitLicenseCenter.Web.Endpoints;

// Контракт snapshot'а активных сеансов: Stage 3 — реальные данные из
// IActiveSessionSnapshotStore, заполняемого ReconciliationJob (cold) и
// HotTierPollingService (hot overlay).
// LicenseStatus (ADR-48, MLC-166) сериализуется строкой через
// JsonStringEnumConverter (как прочие enum'ы API): Consuming/NotConsuming/Pending.
public sealed record SessionSnapshotEntry(
    Guid SessionId,
    Guid ClusterInfobaseId,
    Guid TenantId,
    string TenantName,
    string InfobaseName,
    string AppId,
    string UserName,
    string Host,
    LicenseStatus LicenseStatus,
    DateTime StartedAt,
    int DurationSeconds);

// LicenseFactAvailable (ADR-48): false ⇒ фронт показывает баннер «данные о лицензиях
// недоступны» (факт rac --licenses не получен в цикле, построившем снимок).
public sealed record SessionsSnapshotResponse(
    IReadOnlyList<SessionSnapshotEntry> Items,
    DateTime CapturedAt,
    int TookMs,
    string Source,
    bool LicenseFactAvailable);

public sealed record KillSessionRequest(string? Reason);
