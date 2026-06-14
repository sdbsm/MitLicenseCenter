using MitLicenseCenter.Domain.Common;

namespace MitLicenseCenter.Domain.Infobases;

public sealed class Infobase : IEntity
{
    public Guid Id { get; init; }
    // Settable: смена владельца возможна только через POST /infobases/{id}/reassign
    // (с проверкой коллизии имени и записью в аудит). PUT/форма клиента не меняют.
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public Guid ClusterInfobaseId { get; set; }
    // MLC-088 (single-host): серверное поле снято — SQL-инстанс задаётся одной настройкой
    // Sql.Server. Имя БД остаётся per-база.
    public required string DatabaseName { get; set; }
    public InfobaseStatus Status { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }

    // MLC-151 — токен оптимистической блокировки (зеркаль Tenant/MLC-136). SQL Server
    // `rowversion` (8-байтовый автоинкремент на каждый UPDATE строки). Конкурентный
    // апдейт через PUT /infobases/{id} с устаревшим токеном → DbUpdateConcurrencyException
    // → 409. Nullable: под EF InMemory (тесты) и до первой записи токен не материализуется;
    // SQL Server заполняет существующие строки при добавлении столбца (аддитивная миграция).
    public byte[]? RowVersion { get; set; }
}
