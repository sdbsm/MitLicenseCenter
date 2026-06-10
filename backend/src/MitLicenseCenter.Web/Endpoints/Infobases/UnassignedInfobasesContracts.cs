using System.ComponentModel.DataAnnotations;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-092 — «нераспределённые» базы кластера: есть в кластере 1С, но не заведены в
// панель и не скрыты оператором. Items строится diff'ом по живому (кэшируемому)
// снапшоту RAS; HiddenItems — из таблицы игнор-листа (рендерится и при Available:false).
// CheckedAtUtc — время фактического опроса RAS (не время ответа на запрос).
public sealed record UnassignedInfobaseItemResponse(
    Guid ClusterInfobaseId,
    string Name,
    string? Description);

public sealed record HiddenUnassignedInfobaseResponse(
    Guid ClusterInfobaseId,
    string Name,
    DateTime HiddenAtUtc,
    string HiddenBy);

// MLC-095 — обратный дрейф: запись панели, чьего ClusterInfobaseId нет в снапшоте
// кластера 1С («мёртвая душа»: база удалена из кластера, а в панели висит здоровой).
// Считается ТОЛЬКО при Available:true (сбой опроса RAS ≠ пропавшие базы) — иначе пусто.
public sealed record MissingInfobaseDto(
    Guid InfobaseId,
    string TenantName,
    string Name,
    Guid ClusterInfobaseId);

// Внутренняя проекция записи панели (Id + имя клиента + имя базы + UUID кластера) —
// общая основа прямого (Items) и обратного (MissingItems) diff'ов одним join'ом.
internal sealed record PanelInfobaseRow(
    Guid InfobaseId,
    string TenantName,
    string Name,
    Guid ClusterInfobaseId);

public sealed record UnassignedInfobasesResponse(
    IReadOnlyList<UnassignedInfobaseItemResponse> Items,
    IReadOnlyList<HiddenUnassignedInfobaseResponse> HiddenItems,
    IReadOnlyList<MissingInfobaseDto> MissingItems,
    bool Available,
    string? Error,
    DateTime CheckedAtUtc);

// Name — снапшот имени базы на момент скрытия (блок «Скрытые» рендерится из БД,
// когда RAS недоступен). Лимит 200 — как Infobase.Name; DataAnnotations здесь только
// для Swagger, реальная проверка — в обработчике (гоча minimal API, CLAUDE.md).
public sealed record HideUnassignedInfobaseRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string? Name);
