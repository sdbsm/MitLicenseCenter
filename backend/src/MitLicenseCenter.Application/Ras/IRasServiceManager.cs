namespace MitLicenseCenter.Application.Ras;

// Порт управления локальной службой Windows, под которой работает ras.exe (ADR-47).
// Реализация (Infrastructure) — поверх sc.exe; обнаружение по binPath, содержащему
// ras.exe (имя службы у операторов не стандартизировано). Single-host (ADR-28): хост
// фиксирован localhost. Удаление/останов службы — вне объёма; только register/update/start.
public interface IRasServiceManager
{
    // Диагностика 4 состояний (OK / NotRegistered / Outdated / Stopped) + предпросмотр
    // команды sc нужного действия. Читает целевые параметры из Settings (порт —
    // OneC.RAS.Endpoint, платформа — OneC.DefaultPlatformVersion) и обнаруживает
    // ras.exe выбранной версии. Не мутирует систему.
    Task<RasServiceDiagnosis> DiagnoseAsync(CancellationToken ct);

    // sc create новой службы RAS на ras.exe выбранной платформы, тип auto, порт из
    // endpoint, цель — локальный агент кластера. Возвращает применённое состояние.
    Task<RasServiceOperationResult> RegisterAsync(CancellationToken ct);

    // Перерегистрация существующей службы под новую платформу/порт: stop → sc config
    // (новый binPath/порт) → start. Идемпотентно.
    Task<RasServiceOperationResult> UpdateAsync(CancellationToken ct);

    // sc start остановленной службы.
    Task<RasServiceOperationResult> StartAsync(CancellationToken ct);
}

// Доменное исключение слоя: невозможно выполнить операцию из-за неготовности
// окружения (нет ras.exe выбранной платформы, не задан порт/версия, служба не найдена
// там, где ожидалась). Эндпоинт мапит его в 409 с санитизированным русским текстом.
public sealed class RasServiceOperationException : Exception
{
    public RasServiceOperationException(string message)
        : base(message)
    {
    }

    public RasServiceOperationException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
