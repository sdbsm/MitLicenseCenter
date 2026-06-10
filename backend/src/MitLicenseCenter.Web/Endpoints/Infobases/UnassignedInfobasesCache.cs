using MitLicenseCenter.Application.Clusters;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-092 — singleton-кэш последнего снапшота опроса RAS для GET /infobases/unassigned.
// Один слот по образцу ClusterUuidCache (MLC-041): single-host даёт ровно один кластер.
// TTL — константа 60 с (решение куратора: НЕ ключ настроек, каталог не раздуваем);
// ?refresh=true идёт мимо кэша. Кэшируется только снапшот RAS (вместе с CheckedAtUtc
// фактического опроса) — diff с БД (заведённые/скрытые) считается на каждый запрос,
// поэтому create/hide/unhide видны сразу, без ожидания истечения TTL. Неуспешный опрос
// (Available:false) кэшируется на тот же TTL — выравнивает нагрузку на RAS при сбое;
// Error в слоте уже санитизирован.
internal sealed class UnassignedInfobasesCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private ClusterSnapshot? _snapshot;

    public bool TryGet(DateTime nowUtc, out ClusterSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_snapshot is { } s && nowUtc - s.CheckedAtUtc < Ttl)
            {
                snapshot = s;
                return true;
            }
        }

        snapshot = null!;
        return false;
    }

    public void Store(ClusterSnapshot snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
        }
    }

    internal sealed record ClusterSnapshot(
        IReadOnlyList<ClusterInfobase> Infobases,
        bool Available,
        string? Error,
        DateTime CheckedAtUtc);
}
