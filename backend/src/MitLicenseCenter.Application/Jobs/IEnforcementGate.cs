namespace MitLicenseCenter.Application.Jobs;

/// <summary>
/// Взаимоисключающий замок enforcement'а сессий. Гарантирует, что в любой момент
/// времени kill-логику (<see cref="IKillEnforcer.EnforceAsync"/>) исполняет ровно
/// один путь — cold (Hangfire <c>ReconciliationJob</c>) ИЛИ hot
/// (<c>HotTierPollingService</c>), но не оба одновременно.
/// </summary>
/// <remarks>
/// MLC-044. До MLC-044 enforce'ил только cold-цикл, и over-kill (MLC-001) исключался
/// Hangfire-атрибутом <c>[DisableConcurrentExecution]</c>. Теперь enforce'ит и hot-цикл
/// (BackgroundService, на который Hangfire-фильтр НЕ действует), поэтому нужен общий
/// in-process замок: single-node, один процесс → достаточно <c>SemaphoreSlim(1, 1)</c>.
/// Вызывающий ОБЯЗАН удерживать замок на всё время «fetch свежего списка + kill», чтобы
/// второй путь вошёл уже после kills первого и увидел их через свежий re-fetch
/// (идемпотентный протокол) — это и исключает over-kill.
/// </remarks>
public interface IEnforcementGate
{
    /// <summary>
    /// Асинхронно берёт замок. Возвращает scope, освобождающий замок при <c>Dispose</c>
    /// (использовать с <c>using</c>). Если замок занят — ожидает освобождения.
    /// </summary>
    Task<IDisposable> AcquireAsync(CancellationToken ct);
}
