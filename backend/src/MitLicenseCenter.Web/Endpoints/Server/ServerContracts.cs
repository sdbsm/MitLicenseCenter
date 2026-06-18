using System.ComponentModel.DataAnnotations;

namespace MitLicenseCenter.Web.Endpoints;

// Контракты раздела «Сервер» (MLC-213, ADR-54/55). camelCase на проводе; nullable-поля
// опускаются при null (гоча api-omits-null-fields) — FE-схемы (MLC-214) это учитывают.
// enum'ы (health/state) — строкой, как везде.

// Сводный статус узла: список серверов 1С + сводки RAS/SQL/IIS + общий индикатор здоровья.
public sealed record ServerStatusResponse(
    IReadOnlyList<OneCServerDto> OneCServers,
    RasStatusDto Ras,
    SqlStatusDto Sql,
    IisStatusDto Iis,
    string Overall);

// Одна служба сервера 1С (ragent). platformVersion — best-effort из ImagePath.
public sealed record OneCServerDto(string ServiceName, bool Running, string? PlatformVersion);

// Сводка RAS (только наблюдение — управление в /ras-service/*). available:false + error —
// диагностику снять не удалось.
public sealed record RasStatusDto(
    string State,
    bool Running,
    string? ServiceName,
    bool Available,
    string? Error);

// Сводка локальной службы SQL (только наблюдение — управление SQL вне объёма, ADR-54).
public sealed record SqlStatusDto(
    string? Instance,
    string? ServiceName,
    bool Running,
    bool Available,
    string? Error);

// Сводка IIS (только наблюдение — управление в /api/v1/iis/*).
public sealed record IisStatusDto(string State, bool Available, string? Error);

// Запрос старта сервера 1С: имя службы ragent (whitelist по discovery). Confirm не нужен
// (запуск не разрушителен).
public sealed record OneCServerStartRequest(
    [property: Required] string ServiceName);

// Запрос остановки/перезапуска сервера 1С: имя службы + серверный Confirm-гейт
// (разрушительная операция — прерывает работу всех баз узла).
public sealed record OneCServerStopRequest(
    [property: Required] string ServiceName,
    bool Confirm = false);

// Ответ мутации: имя службы + итоговое верифицированное состояние службы
// ("Running"/"Stopped" — WindowsServiceStatus).
public sealed record ServerOperationResponse(string ServiceName, string FinalStatus);
