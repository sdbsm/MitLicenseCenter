using System.ComponentModel.DataAnnotations;

namespace MitLicenseCenter.Web.Endpoints;

// Контракты управления жизненным циклом IIS (MLC-047, ADR-24). State — строковое имя
// IisObjectState (JsonStringEnumConverter), как и остальные enum'ы на проводе.

// Discovery-элементы (оборачиваются в общий DiscoveryResponse<T> из DiscoveryEndpoints).
public sealed record IisAppPoolDto(string Name, string State);

public sealed record IisSiteStateDto(string SiteName, string State);

// Состояние IIS в целом (служба W3SVC). Available:false — статус не прочитан
// (нет прав / служба не найдена); State тогда "Unknown".
public sealed record IisServerStatusResponse(string State, bool Available, string? Error);

// Цель операции над пулом/сайтом. Имя — в теле (а не в маршруте): у пулов/сайтов
// бывают пробелы и кириллица («Default Web Site»), URL-энкодинг лишний. Confirm
// требуется только для recycle (разрушительная операция) — для start/stop/restart
// токен-подтверждение обеспечивает UI.
public sealed record IisTargetRequest(
    [property: Required] string Name,
    bool Confirm = false);

// iisreset — без цели; Confirm обязателен (роняет все сайты сервера).
public sealed record IisResetRequest(bool Confirm = false);

// Ответ мутации: имя цели + состояние сразу после операции (UI обновит бейдж;
// точное состояние доедет фоновым refetch'ем discovery).
public sealed record IisOperationResponse(string Name, string State);
