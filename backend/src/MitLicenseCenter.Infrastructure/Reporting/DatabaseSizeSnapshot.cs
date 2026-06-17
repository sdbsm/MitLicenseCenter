using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Infrastructure.Reporting;

// Сущность-телеметрия (MLC-185): снимок размера базы данных на момент замера.
// Лежит в Infrastructure (не в Domain) — это замеры, не доменный агрегат, по
// прецеденту LicenseUsageSnapshot/AuditLog. Ключ сопоставления — DatabaseName
// (имя базы переживает изменения привязки к клиенту). TenantId nullable: FK на
// Tenant с OnDelete SetNull — телеметрия переживает удаление тенанта.
// Total (DataBytes + LogBytes) не храним — это вычисляемое в отчёте/UI.
public sealed class DatabaseSizeSnapshot : IEntity
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public string DatabaseName { get; init; } = string.Empty;
    public DateTime SnapshotAtUtc { get; init; }
    public long DataBytes { get; init; }
    public long LogBytes { get; init; }
}
