using System.Collections.Concurrent;
using MitLicenseCenter.Application.Sessions;

namespace MitLicenseCenter.Infrastructure.Jobs;

internal sealed class HotTierRegistry : IHotTierRegistry
{
    private readonly ConcurrentDictionary<Guid, int> _hotTenants = new();

    public bool IsHot(Guid tenantId) => _hotTenants.ContainsKey(tenantId);

    public void Promote(Guid tenantId) => _hotTenants[tenantId] = 0;

    public void Demote(Guid tenantId)
    {
        if (!_hotTenants.TryGetValue(tenantId, out var streak))
            return;

        var next = streak + 1;
        if (next >= 2)
            _hotTenants.TryRemove(tenantId, out _);
        else
            _hotTenants[tenantId] = next;
    }

    public IReadOnlyCollection<Guid> CurrentHotTenants() => _hotTenants.Keys.ToList();
}
