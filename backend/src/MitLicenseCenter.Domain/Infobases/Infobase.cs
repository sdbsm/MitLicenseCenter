using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Domain.Infobases;

public sealed class Infobase : IEntity
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Name { get; set; }
    public Guid ClusterInfobaseId { get; set; }
    public required string DatabaseServer { get; set; }
    public required string DatabaseName { get; set; }
    public InfobaseStatus Status { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }
}
