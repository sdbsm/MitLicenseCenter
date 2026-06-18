namespace MitLicenseCenter.Application.Server;

/// <summary>
/// Итог мутирующей операции над службой Windows. Несёт <b>верифицированное</b>
/// фактическое состояние службы (подтверждённое опросом <c>ServiceController</c>),
/// а не намерение — см. контракт надёжности ADR-55.
/// </summary>
/// <param name="ServiceName">Имя службы Windows, над которой выполнена операция.</param>
/// <param name="FinalStatus">Подтверждённое фактическое состояние после операции.</param>
public sealed record WindowsServiceOperationResult(string ServiceName, WindowsServiceStatus FinalStatus);

/// <summary>
/// Достижимое целевое состояние службы для контракта надёжности (ADR-55). Промежуточные
/// состояния (Starting/Stopping/Paused) сюда не входят — операция верифицирует именно
/// терминальное «работает» / «остановлена».
/// </summary>
public enum WindowsServiceStatus
{
    /// <summary>Служба фактически работает (<c>ServiceControllerStatus.Running</c>).</summary>
    Running,

    /// <summary>Служба фактически остановлена.</summary>
    Stopped,
}
