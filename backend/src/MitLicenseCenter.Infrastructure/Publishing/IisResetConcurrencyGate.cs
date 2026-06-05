using MitLicenseCenter.Application.Publishing;

namespace MitLicenseCenter.Infrastructure.Publishing;

// MLC-047: сериализатор разрушительных IIS-операций (см. IIisResetConcurrencyGate).
// Singleton — один процесс на single-node, in-process SemaphoreSlim(1, 1). N=1
// сознательно: recycle/start/stop/iisreset роняют сайты/пулы, их нельзя пересекать.
// Структурно зеркалит WebinstConcurrencyGate (тот N=3), но семантика иная.
internal sealed class IisResetConcurrencyGate : IIisResetConcurrencyGate, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> AcquireAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        return new Releaser(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;

        public void Dispose()
        {
            // Идемпотентно: повторный Dispose не должен дважды Release'ить семафор.
            Interlocked.Exchange(ref _semaphore, null)?.Release();
        }
    }
}
