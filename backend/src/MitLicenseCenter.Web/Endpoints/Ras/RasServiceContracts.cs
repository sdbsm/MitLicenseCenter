namespace MitLicenseCenter.Web.Endpoints;

// Контракты эндпоинтов управления службой RAS (MLC-159, ADR-47). camelCase на проводе;
// nullable-поля опускаются при null (см. гочу api-omits-null-fields) — FE-схемы это
// учитывают (parity в MLC-160).

// Снимок обнаруженной службы RAS для статуса. Все поля, кроме имени и факта running,
// best-effort (могут отсутствовать, если binPath/вывод sc непарсимы).
public sealed record RasServiceInfoDto(
    string ServiceName,
    bool IsRunning,
    string? BinPath,
    string? PlatformVersion,
    string? Port);

// Целевые параметры (что мы хотим видеть в службе): путь к ras.exe выбранной платформы,
// версия, порт (из OneC.RAS.Endpoint), адрес локального агента кластера.
public sealed record RasServiceTargetDto(
    string RasExePath,
    string PlatformVersion,
    string Port,
    string AgentAddress);

// Ответ GET /ras-service/status. state — одно из 4 значений (см. RasServiceState):
// "Ok" | "NotRegistered" | "Outdated" | "Stopped". commandPreview — точная команда sc
// рекомендованного действия (прозрачность ADR-47); targetReady=false + issue — окружение
// не готово (нет ras.exe/не задана платформа), действие выполнить нельзя.
public sealed record RasServiceStatusResponse(
    string State,
    RasServiceInfoDto? Service,
    RasServiceTargetDto? Target,
    string? CommandPreview,
    bool TargetReady,
    string? Issue);

// Ответ на успешную операцию register/update/start: применённое состояние + имя службы.
public sealed record RasServiceOperationResponse(
    string State,
    string ServiceName);
