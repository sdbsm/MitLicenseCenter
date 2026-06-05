namespace MitLicenseCenter.Application.Clusters;

// Кросс-вызовный кэш резолва UUID кластера (MLC-041 / PERF-05). Single-node →
// ровно один кластер (records[0] из «cluster list»), UUID стабилен между вызовами
// rac.exe, поэтому хранится один слот. Снимает лишний «cluster list» перед каждым
// session list / session terminate / infobase summary list.
// Реализация — ClusterUuidCache (singleton) в Infrastructure/Clusters/.
public interface IClusterUuidCache
{
    // Хит только если ключ совпал с закэшированным и UUID не null.
    bool TryGet(in ClusterUuidKey key, out string? uuid);

    // Публикует успешно резолвленный UUID. Неуспех/null кэшировать нельзя.
    void Store(in ClusterUuidKey key, string uuid);

    // Сбрасывает слот, если он принадлежит этому ключу (stale-UUID / смена входных
    // параметров RAS). Чужой ключ не трогает.
    void Invalidate(in ClusterUuidKey key);
}

// Ключ кэша: путь rac.exe + RAS-endpoint. «cluster list» идёт без auth (BuildArgs),
// идентичность кластера от creds не зависит — поэтому creds в ключ не входят.
// record struct → структурное равенство строк (ordinal по умолчанию).
public readonly record struct ClusterUuidKey(string ExePath, string Endpoint);
