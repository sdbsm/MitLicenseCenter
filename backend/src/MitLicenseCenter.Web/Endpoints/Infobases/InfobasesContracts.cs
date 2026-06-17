using System.ComponentModel.DataAnnotations;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;

namespace MitLicenseCenter.Web.Endpoints;

public sealed record InfobaseResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    Guid ClusterInfobaseId,
    string DatabaseName,
    InfobaseStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // MLC-151 — токен оптимистической блокировки (зеркаль TenantResponse/MLC-136).
    // byte[] сериализуется как base64; при null поле опускается (WhenWritingNull).
    // Под EF InMemory токен не материализуется → omittable на фронте.
    byte[]? RowVersion = null);

public sealed record InfobaseListItemResponse(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string Name,
    Guid ClusterInfobaseId,
    string DatabaseName,
    InfobaseStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    PublicationResponse Publication,
    // MLC-185d — текущий размер базы из ПОСЛЕДНЕГО снимка телеметрии (DatabaseSizeSnapshot
    // по DatabaseName, max SnapshotAtUtc). Allocated, байты; UI показывает сумму
    // (Data+Log) форматтером КБ/МБ/ГБ. null (опускается на проводе, WhenWritingNull):
    // снимка ещё нет — ночная джоба не отработала / база была недоступна при замере.
    long? CurrentDataBytes = null,
    long? CurrentLogBytes = null,
    // MLC-151 — токен инфобазы для формы редактирования (открывается из элемента списка):
    // без него FE прислал бы null → OriginalValue не выставится → защита от гонки молча не
    // сработала бы. omit-null: под InMemory токен не материализуется.
    byte[]? RowVersion = null);

// MLC-150 — ClusterAvailable заполняется ТОЛЬКО при фильтре notInCluster=true: это
// признак доступности снапшота RAS, по которому отбираются «не найденные в кластере»
// записи. null (опускается на проводе) при фильтре по статусу/клиенту/без фильтра —
// доступность кластера в этих случаях нерелевантна. false при notInCluster=true и
// недоступном RAS: фронт показывает честное «не удалось проверить кластер», а не
// вводящий в заблуждение пустой список (нельзя отличить «нет пропавших» от «не знаем»).
public sealed record InfobaseListResponse(
    IReadOnlyList<InfobaseListItemResponse> Items,
    int Total,
    int Page,
    int PageSize,
    bool? ClusterAvailable = null);

public sealed record InfobaseDetailResponse(
    InfobaseResponse Infobase,
    PublicationResponse Publication);

// MLC-181c — облегчённый id-набор для bulk-операции «Выбрать все N по фильтру».
// Лёгкие строки (id публикации + минимум полей для label диалогов), БЕЗ пагинации,
// по ТОМУ ЖЕ фильтру, что и список (общий BuildFilteredQuery). FE наполняет ими тот же
// внешний выбор, существующий per-id bulk-движок применяет действие.
// capped=true ⇒ пригодных строк больше MaxBulkIds: items усечён до кэпа, total — реальное
// число; FE по capped отказывается выбирать и просит уточнить фильтр.
public sealed record InfobaseBulkIdItem(
    Guid InfobaseId,
    Guid PublicationId,
    string InfobaseName,
    string SiteName,
    string VirtualPath);

public sealed record InfobaseBulkIdsResponse(
    IReadOnlyList<InfobaseBulkIdItem> Items,
    int Total,
    bool Capped);

// Точечная проверка занятости базы кластера: одна база кластера принадлежит ровно
// одному клиенту (IX_Infobases_ClusterInfobaseId). Фронт дёргает её при выборе базы,
// чтобы не выгружать весь список инфобаз ради проверки уникальности (MLC-015).
public sealed record ClusterIdAvailabilityResponse(
    bool Taken,
    string? TakenByTenantName);

public sealed record CreateInfobaseRequest(
    [property: Required] Guid TenantId,
    [property: Required, StringLength(InfobaseValidationRules.NameMaxLength, MinimumLength = 1)] string Name,
    [property: Required] Guid ClusterInfobaseId,
    [property: Required, StringLength(InfobaseValidationRules.DatabaseNameMaxLength, MinimumLength = 1)] string DatabaseName,
    [property: Required] InfobaseStatus Status,
    [property: Required] CreatePublicationRequest Publication);

public sealed record UpdateInfobaseRequest(
    [property: Required, StringLength(InfobaseValidationRules.NameMaxLength, MinimumLength = 1)] string Name,
    [property: Required] Guid ClusterInfobaseId,
    [property: Required, StringLength(InfobaseValidationRules.DatabaseNameMaxLength, MinimumLength = 1)] string DatabaseName,
    [property: Required] InfobaseStatus Status,
    [property: Required] UpdatePublicationRequest Publication,
    // MLC-151 — токен оптимистической блокировки инфобазы, прочитанный клиентом при загрузке
    // формы. ОПЦИОНАЛЕН: null оставляет обратную совместимость (старые клиенты / InMemory-тесты).
    // Непустой токен выставляется как OriginalValue → конкурентный апдейт → 409.
    // Вложенный Publication.RowVersion защищает публикацию в составе того же aggregate-апдейта.
    byte[]? RowVersion = null);

public sealed record ReassignInfobaseRequest(
    [property: Required] Guid TargetTenantId);

internal static class InfobaseMappings
{
    public static InfobaseResponse ToResponse(this Infobase x) =>
        new(x.Id, x.TenantId, x.Name, x.ClusterInfobaseId, x.DatabaseName, x.Status, x.CreatedAt, x.UpdatedAt,
            RowVersion: x.RowVersion);

    public static InfobaseDetailResponse ToDetailResponse(this Infobase infobase, Publication publication) =>
        new(infobase.ToResponse(), publication.ToResponse());
}
