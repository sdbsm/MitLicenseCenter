using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using MitLicenseCenter.Application.Settings;

namespace MitLicenseCenter.Infrastructure.Settings;

// Singleton in-memory snapshot с TTL ≈ 30s. Hot-path адаптеры PR 3.2/3.3 читают
// конфигурацию через GetString/GetInt — это убирает DB-hit с каждого poll-tick'а.
// Scoped зависимости (AppDbContext, ISettingsStore) получаем через
// IServiceScopeFactory: дёргать scoped из singleton конструктора напрямую — антипаттерн.
internal sealed class SettingsSnapshot : ISettingsSnapshot
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();

    private Dictionary<string, string?>? _cache;
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;

    public SettingsSnapshot(IServiceScopeFactory scopeFactory, TimeProvider clock)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
    }

    public string? GetString(string key)
    {
        var cache = EnsureLoaded();
        return cache.TryGetValue(key, out var value) ? value : null;
    }

    public int? GetInt(string key)
    {
        var raw = GetString(key);
        if (raw is null)
        {
            return null;
        }
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _cache = null;
            _loadedAt = DateTimeOffset.MinValue;
        }
    }

    private Dictionary<string, string?> EnsureLoaded()
    {
        lock (_gate)
        {
            var now = _clock.GetUtcNow();
            if (_cache is not null && now - _loadedAt < Ttl)
            {
                return _cache;
            }

            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();

            // ListAsync маскирует секреты до null, поэтому грузим через явный Get для каждого
            // ключа из catalog'а — это даёт hot-path доступ к расшифрованным секретам тоже.
            var snapshot = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var key in SettingDefinitions.All.Keys)
            {
                snapshot[key] = store.GetAsync(key).GetAwaiter().GetResult();
            }

            _cache = snapshot;
            _loadedAt = now;
            return snapshot;
        }
    }
}
