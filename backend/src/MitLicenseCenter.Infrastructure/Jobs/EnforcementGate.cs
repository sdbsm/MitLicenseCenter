using MitLicenseCenter.Application.Jobs;

namespace MitLicenseCenter.Infrastructure.Jobs;

// MLC-044: общий взаимоисключающий замок enforcement'а (см. IEnforcementGate).
// Singleton — один процесс на single-node, поэтому in-process SemaphoreSlim(1, 1)
// достаточно (распределённый лок Hangfire покрывает только cold-vs-cold; hot —
// BackgroundService, вне Hangfire-фильтров).
internal sealed class EnforcementGate : IEnforcementGate, IDisposable
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
