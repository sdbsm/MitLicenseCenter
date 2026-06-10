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
}
