namespace MitLicenseCenter.Web.Endpoints;

// Контракт snapshot'а активных сеансов: в Stage 2 endpoint возвращает пустой
// список — реальные данные приходят из 1C Cluster REST API в Stage 3.
// Поля заводим уже сейчас, чтобы клиент мог реализовать UI против стабильной схемы.
public sealed record SessionSnapshotEntry(
    Guid SessionId,
    Guid InfobaseId,
    Guid TenantId,
    string AppId,
    bool ConsumesLicense,
    DateTime StartedAt);

public sealed record SessionsSnapshotResponse(
    IReadOnlyList<SessionSnapshotEntry> Items,
    DateTime CapturedAt,
    int TookMs);
