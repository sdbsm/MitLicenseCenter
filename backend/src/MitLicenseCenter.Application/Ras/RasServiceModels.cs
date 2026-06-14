namespace MitLicenseCenter.Application.Ras;

// Четыре состояния диагностики службы RAS (ADR-47). Маппинг состояния → доступное
// действие задаётся на стороне UI; backend лишь классифицирует и отдаёт предпросмотр
// нужной команды sc.
public enum RasServiceState
{
    // Служба есть, запущена, binPath на актуальной платформе и порт = OneC.RAS.Endpoint.
    Ok = 0,

    // Службы с ras.exe в binPath не найдено → предложить регистрацию (register → 600).
    NotRegistered = 1,

    // Служба есть, но binPath на устаревшей версии ИЛИ порт ≠ endpoint →
    // перерегистрация (update → 601).
    Outdated = 2,

    // Служба есть, но остановлена → запуск (start → 602).
    Stopped = 3,
}

// Снимок обнаруженной службы RAS (по binPath, содержащему ras.exe). Все поля,
// кроме факта существования, best-effort: если sc qc вернул неполный/непарсимый
// вывод, отдельные значения могут быть null.
public sealed record DiscoveredRasService(
    string ServiceName,
    bool IsRunning,
    string? BinPath,
    string? PlatformVersion,
    string? Port);

// Что мы хотим увидеть в службе RAS: путь к ras.exe выбранной платформы, целевая
// версия платформы, порт (из OneC.RAS.Endpoint) и адрес локального агента кластера.
public sealed record RasServiceTarget(
    string RasExePath,
    string PlatformVersion,
    string Port,
    string AgentAddress);

// Результат диагностики для эндпоинта статуса. Несёт состояние, снимок найденной
// службы (если есть), целевые параметры и предпросмотр команды sc для каждого
// применимого действия (прозрачность + воспроизводимость оператором, ADR-47).
public sealed record RasServiceDiagnosis(
    RasServiceState State,
    DiscoveredRasService? Service,
    RasServiceTarget? Target,
    string? CommandPreview,
    bool TargetReady,
    string? Issue);

// Тип операции над службой RAS. Удаление/останов — вне объёма (ADR-47).
public enum RasServiceOperation
{
    Register,
    Update,
    Start,
}

// Итог выполнения операции: применённое состояние службы, имя службы и применённые
// платформа/порт (для записи аудита — без секретов; служба слушает loopback,
// obj/password не задаём). Для Start платформа/порт берутся из обнаруженной службы и
// могут быть null (мы её не перенастраивали).
public sealed record RasServiceOperationResult(
    RasServiceState State,
    string ServiceName,
    string? PlatformVersion,
    string? Port);
