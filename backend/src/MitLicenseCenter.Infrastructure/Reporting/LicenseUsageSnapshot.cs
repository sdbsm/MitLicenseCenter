using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Infrastructure.Reporting;

// Сущность-телеметрия (MLC-048, ADR-25): агрегат потребления лицензий одним тенантом
// за закрытый 15-минутный бакет. Лежит в Infrastructure (не в Domain) — это замеры, не
// доменный агрегат, по прецеденту AuditLog. TenantId nullable: FK на Tenant с OnDelete
// SetNull — телеметрия переживает удаление тенанта (как AuditLog).
public sealed class LicenseUsageSnapshot : IEntity
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public DateTime BucketStartUtc { get; init; }
    public int ConsumedMin { get; init; }
    public int ConsumedMax { get; init; }
    public double ConsumedAvg { get; init; }
    public int Limit { get; init; }
}
