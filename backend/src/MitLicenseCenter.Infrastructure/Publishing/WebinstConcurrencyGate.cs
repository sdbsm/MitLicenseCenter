using MitLicenseCenter.Application.Publishing;

namespace MitLicenseCenter.Infrastructure.Publishing;

// MLC-046: ограничитель одновременных спавнов webinst.exe (см. IWebinstConcurrencyGate).
// Singleton — один процесс на single-node, поэтому in-process SemaphoreSlim(N, N)
// достаточно. N подобран малым (3): webinst тяжёлый (создание IIS-приложения + запись
// vrd/web.config), пачка из ~100 публикаций не должна положить сервер роем процессов
// (семья ADR-3.3). Промоут N в SettingDefinitions — отложенная опция (сейчас константа,
// как Timeout в OneCWebinstPublisher).
internal sealed class WebinstConcurrencyGate : IWebinstConcurrencyGate, IDisposable
{
    internal const int MaxConcurrency = 3;

    private readonly SemaphoreSlim _semaphore = new(MaxConcurrency, MaxConcurrency);

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
