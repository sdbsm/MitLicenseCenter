using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Domain.Publications;

public sealed class Publication : IEntity
{
    public Guid Id { get; init; }
    public Guid InfobaseId { get; init; }
    public required string SiteName { get; set; }
    public required string VirtualPath { get; set; }
    public required string PlatformVersion { get; set; }
    public bool EnableOData { get; set; }
    public bool EnableHttpServices { get; set; }
    public string? VrdCustomXml { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }
}
