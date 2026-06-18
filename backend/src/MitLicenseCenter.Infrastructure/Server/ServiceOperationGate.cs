using System.Collections.Concurrent;
using MitLicenseCenter.Application.Server;

namespace MitLicenseCenter.Infrastructure.Server;

// Per-service-name сериализатор мутаций службы (ADR-55, MLC-212; см.
// IServiceOperationGate). Singleton — словарь семафоров живёт на весь процесс
// (single-node). На каждое имя службы — отдельный SemaphoreSlim(1, 1): мутации одной
// службы строго по одной, разные службы независимы. Ключ — имя службы, регистр
// игнорируется (имена служб Windows регистронезависимы). Структурно зеркалит
// IisResetConcurrencyGate (тот глобальный N=1), но замок здесь по ключу.
internal sealed class ServiceOperationGate : IServiceOperationGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<IDisposable> AcquireAsync(string serviceName, CancellationToken ct)
    {
        var semaphore = _gates.GetOrAdd(serviceName, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        return new Releaser(semaphore);
    }

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
