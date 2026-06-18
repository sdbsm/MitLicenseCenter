namespace MitLicenseCenter.Application.Server;

// Модели сводного статуса служб узла (MLC-213, ADR-54/55). Чистые Application-record'ы
// без инфраструктурных типов; nullable-поля опускаются на проводе при null (гоча
// api-omits-null-fields) — FE-схемы (MLC-214) это учитывают. Каждая под-сводка несёт
// флаг доступности своего адаптера: сбой одного источника отражается Available:false +
// Error, а не падением всего снимка (паттерн discovery IIS, MLC-047).

// Снимок одной службы сервера 1С (ragent). Несколько установленных версий платформы =
// несколько служб. PlatformVersion — best-effort из ImagePath (может отсутствовать,
// если путь нестандартный).
public sealed record OneCServerStatus(string ServiceName, bool Running, string? PlatformVersion);

// Сводка RAS для агрегатора (только наблюдение — управление остаётся в /ras-service/*).
// State — строковое имя RasServiceState ("Ok"/"NotRegistered"/"Outdated"/"Stopped").
// ServiceName — имя обнаруженной службы (null, если не зарегистрирована). Available:false
// + Error — диагностику RAS снять не удалось.
public sealed record RasStatusSummary(
    string State,
    bool Running,
    string? ServiceName,
    bool Available,
    string? Error);

// Сводка локальной службы SQL (только наблюдение — управление SQL вне объёма, ADR-54).
// Instance — имя инстанса (best-effort через ISqlInstanceDiscovery). ServiceName — имя
// службы sqlservr (null, если не найдена). Available:false + Error — статус снять не удалось.
public sealed record SqlStatusSummary(
    string? Instance,
    string? ServiceName,
    bool Running,
    bool Available,
    string? Error);

// Сводка IIS для агрегатора (только наблюдение — управление остаётся в /api/v1/iis/*).
// State — строковое имя IisObjectState ("Started"/"Stopped"/...). Available:false + Error —
// состояние службы W3SVC снять не удалось.
public sealed record IisStatusSummary(string State, bool Available, string? Error);

// Сводный индикатор здоровья узла — простая эвристика «светофора» для FE (MLC-214):
//   Down     — ни одной запущенной службы ragent (сервер 1С — ядро узла);
//   Degraded — есть запущенный ragent, но что-то не так (RAS/SQL/IIS не в норме либо
//              какой-то адаптер недоступен);
//   Healthy  — все источники доступны и в норме;
//   Unknown  — вообще ничего не удалось опросить (все адаптеры недоступны).
public enum ServerHealth
{
    Unknown,
    Healthy,
    Degraded,
    Down,
}

// Сводный снимок статуса служб узла: список серверов 1С + сводки RAS/SQL/IIS + общий
// индикатор здоровья.
public sealed record ServerStatusSnapshot(
    IReadOnlyList<OneCServerStatus> OneCServers,
    RasStatusSummary Ras,
    SqlStatusSummary Sql,
    IisStatusSummary Iis,
    ServerHealth Overall);
