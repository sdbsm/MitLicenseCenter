namespace MitLicenseCenter.Application.Server;

/// <summary>
/// Сериализатор мутирующих операций над службой Windows <b>по имени службы</b>
/// (ADR-55, п. 4). Мутации одной и той же службы (старт/стоп/рестарт) исполняются
/// строго по одной за раз; операции над <i>разными</i> службами независимы и не
/// блокируют друг друга.
/// </summary>
/// <remarks>
/// В отличие от глобального <see cref="MitLicenseCenter.Application.Publishing.IIisResetConcurrencyGate"/>
/// (один слот N=1 на весь процесс — разрушительные IIS-операции нельзя пересекать
/// вообще), здесь замок <b>по ключу-имени</b>: на каждую службу свой
/// <c>SemaphoreSlim(1, 1)</c>, поэтому старт службы A и стоп службы B идут параллельно.
/// Single-node, один процесс → достаточно in-process семафоров.
/// </remarks>
public interface IServiceOperationGate
{
    /// <summary>
    /// Асинхронно берёт слот для указанной службы. Возвращает scope, освобождающий
    /// слот при <c>Dispose</c> (использовать с <c>using</c>). Слот этой службы занят —
    /// ожидает освобождения; слоты других служб не затрагиваются.
    /// </summary>
    Task<IDisposable> AcquireAsync(string serviceName, CancellationToken ct);
}
