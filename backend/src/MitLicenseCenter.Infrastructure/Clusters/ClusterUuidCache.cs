using MitLicenseCenter.Application.Clusters;

namespace MitLicenseCenter.Infrastructure.Clusters;

// Singleton-кэш UUID кластера (MLC-041 / PERF-05). Один слот — single-node даёт
// ровно один кластер. Дёргается из cold-джоба (ReconciliationJob) и hot-сервиса
// (HotTierPollingService) одновременно → доступ к слоту под коротким lock.
// Спавн «cluster list» делает адаптер ВНЕ lock; здесь только публикация/чтение слота,
// поэтому редкий двойной резолв на cold-старте допустим и самоисцеляется.
internal sealed class ClusterUuidCache : IClusterUuidCache
{
    private readonly object _gate = new();
    private ClusterUuidKey? _key;
    private string? _uuid;

    public bool TryGet(in ClusterUuidKey key, out string? uuid)
    {
        lock (_gate)
        {
            if (_key is { } k && k.Equals(key) && _uuid is not null)
            {
                uuid = _uuid;
                return true;
            }
        }

        uuid = null;
        return false;
    }

    public void Store(in ClusterUuidKey key, string uuid)
    {
        lock (_gate)
        {
            _key = key;
            _uuid = uuid;
        }
    }

    public void Invalidate(in ClusterUuidKey key)
    {
        lock (_gate)
        {
            if (_key is { } k && k.Equals(key))
            {
                _key = null;
                _uuid = null;
            }
        }
    }
}
