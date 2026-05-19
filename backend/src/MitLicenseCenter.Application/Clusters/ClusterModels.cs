namespace MitLicenseCenter.Application.Clusters;

// Сеанс, полученный с 1С Cluster REST API. ClusterInfobaseId — внутренний GUID
// инфобазы в кластере (совпадает с Infobase.ClusterInfobaseId в нашей схеме).
public sealed record ClusterSession(
    Guid SessionId,
    Guid ClusterInfobaseId,
    string AppId,
    string UserName,
    string Host,
    bool ConsumesLicense,
    DateTime StartedAtUtc);

// Минимальный дескриптор для идемпотентного kill: тройка (InfobaseId, SessionId,
// StartedAt) позволяет убедиться, что убиваем именно тот сеанс, который видели
// в снапшоте, а не новый с тем же session-ID.
public sealed record SessionDescriptor(
    Guid ClusterInfobaseId,
    Guid SessionId,
    string AppId,
    DateTime StartedAtUtc);

public sealed record KillSessionResult(bool Killed, bool AlreadyGone);

public sealed record ClusterPingResult(bool Ok, string? Error);
