namespace MitLicenseCenter.Application.Publishing;

/// <summary>
/// Сериализатор разрушительных IIS-операций (MLC-047). Гарантирует, что recycle/start/
/// stop пула/сайта и <c>iisreset</c> исполняются строго по одной за раз — два
/// одновременных <c>iisreset</c> (или recycle во время iisreset) недопустимы.
/// </summary>
/// <remarks>
/// В отличие от <see cref="IWebinstConcurrencyGate"/> (разрешает N=3 параллельных
/// спавнов), здесь N=1: операции глобально-разрушительны (роняют сайты/пулы), а не
/// просто тяжелы. Discovery (list) замок не берёт — чтение состояния не блокируется.
/// Single-node, один процесс → достаточно in-process <c>SemaphoreSlim(1, 1)</c>.
/// </remarks>
public interface IIisResetConcurrencyGate
{
    /// <summary>
    /// Асинхронно берёт единственный слот. Возвращает scope, освобождающий слот при
    /// <c>Dispose</c> (использовать с <c>using</c>). Слот занят — ожидает освобождения.
    /// </summary>
    Task<IDisposable> AcquireAsync(CancellationToken ct);
}
