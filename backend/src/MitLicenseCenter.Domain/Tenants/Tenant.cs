using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Domain.Tenants;

public sealed class Tenant : IEntity
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public int MaxConcurrentLicenses { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }

    // MLC-136 (R12c) — токен оптимистической блокировки. SQL Server `rowversion`
    // (8-байтовый автоинкремент на каждый UPDATE строки). Конкурентный апдейт с
    // устаревшим токеном → DbUpdateConcurrencyException → 409. Nullable: под EF InMemory
    // (тесты) и до первой записи на стороне БД токен не материализуется; SQL Server сам
    // заполняет существующие строки при добавлении столбца (аддитивная миграция).
    public byte[]? RowVersion { get; set; }
}
