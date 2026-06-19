namespace MitLicenseCenter.Application.Server;

/// <summary>
/// Исход рестарта рабочего процесса 1С (<c>rphost</c>) по <c>Pid</c> (MLC-220, ADR-56).
/// Рестарт = завершение ОС-процесса <c>rphost</c>; кластер 1С авто-поднимает новый процесс
/// с другим <c>Pid</c>. Эндпоинт мапит исход в HTTP-код (см. <c>ServerEndpoints</c>).
/// </summary>
public enum OneCProcessRestartOutcome
{
    /// <summary>
    /// Успех: старый <c>Pid</c> исчез из <c>rac process list</c> в пределах таймаута
    /// верификации (кластер заменил процесс) — либо процесс уже отсутствовал на входе
    /// (идемпотентность: нечего делать). Правдивый результат, эндпоинт → 200.
    /// </summary>
    Restarted,

    /// <summary>
    /// <c>Pid</c> отсутствует в текущем <c>rac process list</c> (whitelist не пройден).
    /// Нельзя завершать произвольный ОС-процесс — эндпоинт → 404. (Отдельно от
    /// «уже исчез»: тут процесс изначально не принадлежит кластеру 1С.)
    /// </summary>
    NotInCluster,

    /// <summary>
    /// Guard от переиспользования <c>Pid</c>: ОС-процесс с этим <c>Pid</c> существует, но
    /// это НЕ <c>rphost</c> (ОС успела переназначить номер другому процессу). Завершать
    /// нельзя — эндпоинт → 409 (конфликт состояния).
    /// </summary>
    PidReused,

    /// <summary>
    /// Завершение отправлено, но старый <c>Pid</c> НЕ исчез из <c>rac process list</c> за
    /// таймаут верификации (кластер не заменил процесс). Управляемая ошибка — эндпоинт →
    /// 409, не 500 и не зависание.
    /// </summary>
    VerificationTimedOut,
}

/// <summary>
/// Результат рестарта рабочего процесса 1С: исход + завершённый <c>Pid</c> (для аудита/
/// сообщения). Без секретов.
/// </summary>
public sealed record OneCProcessRestartResult(OneCProcessRestartOutcome Outcome, int Pid);

/// <summary>
/// Рестарт рабочего процесса 1С (<c>rphost</c>) мягким способом по <c>Pid</c> (MLC-220,
/// ADR-56). Контракт безопасности: whitelist по <c>rac process list</c> → guard по имени
/// ОС-процесса → kill → верификация исчезновения <c>Pid</c> с таймаутом. Идемпотентность:
/// уже отсутствующий <c>Pid</c> = успех.
/// </summary>
public interface IOneCProcessRestartService
{
    Task<OneCProcessRestartResult> RestartAsync(int pid, CancellationToken ct);
}
