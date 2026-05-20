namespace MitLicenseCenter.Application.Sessions;

public interface IHotTierRegistry
{
    bool IsHot(Guid tenantId);
    void Promote(Guid tenantId);
    void Demote(Guid tenantId);
    IReadOnlyCollection<Guid> CurrentHotTenants();
}
