using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Domain.Tenants;

public sealed class Tenant : IEntity
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public int MaxConcurrentLicenses { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; init; }
}
