namespace MitLicenseCenter.Application.Server;

/// <summary>
/// Универсальный контроллер мутирующих операций над службой Windows по её имени
/// (старт / стоп / рестарт) с контрактом надёжности ADR-55. Реализация
/// (Infrastructure) отправляет команду (<c>sc start</c> / <c>sc stop</c>) и
/// <b>верифицирует фактическое состояние</b> службы (<see cref="System.ServiceProcess.ServiceController"/>)
/// опросом до целевого в пределах таймаута — возвращается достоверный итог, а не
/// факт отправки команды.
/// </summary>
/// <remarks>
/// Обобщает RAS-паттерн (<c>IRasServiceManager</c>) на любую службу узла. Single-host
/// (ADR-28): управляется только локальный узел. Эндпоинты/маппинг доменного исключения
/// в 409 — отдельный слой (MLC-213+); здесь только Infrastructure + контракт.
/// Мутации одной службы сериализуются через <see cref="IServiceOperationGate"/>.
/// </remarks>
public interface IWindowsServiceController
{
    /// <summary>
    /// Запускает службу и ждёт фактического перехода в «работает» в пределах таймаута.
    /// Уже запущенная служба (<c>sc</c> 1056) — успех. По истечении таймаута без
    /// перехода — <see cref="WindowsServiceOperationException"/>.
    /// </summary>
    Task<WindowsServiceOperationResult> StartAsync(string serviceName, CancellationToken ct);

    /// <summary>
    /// Останавливает службу и ждёт фактического перехода в «остановлена» в пределах
    /// таймаута. Уже остановленная служба (<c>sc</c> 1062) — успех. По истечении
    /// таймаута без перехода — <see cref="WindowsServiceOperationException"/>.
    /// </summary>
    Task<WindowsServiceOperationResult> StopAsync(string serviceName, CancellationToken ct);

    /// <summary>
    /// Перезапускает службу: верифицированный стоп, затем верифицированный старт.
    /// Оба шага — под единым захватом <see cref="IServiceOperationGate"/> (между ними
    /// замок не отпускается). Итог — <see cref="WindowsServiceStatus.Running"/>.
    /// </summary>
    Task<WindowsServiceOperationResult> RestartAsync(string serviceName, CancellationToken ct);
}
