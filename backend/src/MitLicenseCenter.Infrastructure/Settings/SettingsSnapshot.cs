using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using MitLicenseCenter.Application.Settings;

namespace MitLicenseCenter.Infrastructure.Settings;

// Singleton in-memory snapshot с TTL ≈ 30s. Hot-path адаптеры PR 3.2/3.3 читают
// конфигурацию через GetString/GetInt — это убирает DB-hit с каждого poll-tick'а.
// Scoped зависимости (AppDbContext, ISettingsStore) получаем через
// IServiceScopeFactory: дёргать scoped из singleton конструктора напрямую — антипаттерн.
//
// Concurrency (MLC-010): прогретый кэш читается БЕЗ лока через volatile-ссылку на
// неизменяемое CacheState — горячий путь lock-light и не ходит в БД. Обращение к БД
// идёт ВНЕ лока: первый «холодный»/просроченный читатель назначается загрузчиком
// (под коротким локом публикует общий TaskCompletionSource), остальные ждут ТОТ ЖЕ
// Task — single-flight, поэтому конкурентные читатели не грузят дважды и не блокируют
// друг друга на самом DB-вызове. Свежий словарь грузится одним запросом
// (ISettingsStore.GetAllAsync, с расшифровкой секретов) и подменяет _state под коротким
// локом. Блокирующее ожидание остаётся только на холодном чтении (интерфейс
// ISettingsSnapshot синхронный) и минимизировано: один загрузчик, один запрос.
internal sealed class SettingsSnapshot : ISettingsSnapshot
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();

    // Неизменяемый снимок: ссылка подменяется целиком, словарь после публикации не
    // мутируется — отсюда безопасное lock-free чтение горячего пути.
    private CacheState? _state;
    private Task<Dictionary<string, string?>>? _inFlight;
    private long _version;

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
            _state = null;
            // Бамп версии гасит in-flight загрузку, начатую до Invalidate (она могла
            // прочитать БД ещё до коммита SetAsync): её результат не будет опубликован,
            // следующее чтение перезагрузит свежие данные.
            _version++;
        }
    }

    private Dictionary<string, string?> EnsureLoaded()
    {
        // Горячий путь: прогретый кэш в пределах TTL — без лока и без БД.
        var state = Volatile.Read(ref _state);
        if (state is not null && _clock.GetUtcNow() - state.LoadedAt < Ttl)
        {
            return state.Values;
        }

        Task<Dictionary<string, string?>> load;
        TaskCompletionSource<Dictionary<string, string?>>? mine = null;
        var version = 0L;
        lock (_gate)
        {
            // Double-check под локом: пока ждали лок, другой читатель мог прогреть кэш.
            state = _state;
            if (state is not null && _clock.GetUtcNow() - state.LoadedAt < Ttl)
            {
                return state.Values;
            }

            // Single-flight: первый читатель публикует общий Task и становится
            // загрузчиком; остальные подхватывают тот же Task и просто ждут.
            if (_inFlight is null)
            {
                mine = new TaskCompletionSource<Dictionary<string, string?>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _inFlight = mine.Task;
                version = _version;
            }

            load = _inFlight;
        }

        if (mine is not null)
        {
            // Назначенный загрузчик: DB-вызов ВНЕ лока, ровно один на всю пачку читателей.
            RunLoad(mine, version);
        }

        // Ждём результат вне лока — критическая секция DB-IO не держит.
        return load.GetAwaiter().GetResult();
    }

    private void RunLoad(TaskCompletionSource<Dictionary<string, string?>> completion, long version)
    {
        try
        {
            var values = Load();

            lock (_gate)
            {
                _inFlight = null;
                // Публикуем только если за время загрузки не было Invalidate: иначе
                // данные могли устареть относительно только что записанного значения.
                if (_version == version)
                {
                    _state = new CacheState(values, _clock.GetUtcNow());
                }
            }

            completion.SetResult(values);
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _inFlight = null;
            }
            completion.SetException(ex);
        }
    }

    private Dictionary<string, string?> Load()
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();

        // Один запрос на все строки + расшифровка секретов (вместо N отдельных Get).
        // Синхронное ожидание здесь — только на холодном пути и только у загрузчика.
        var all = store.GetAllAsync().GetAwaiter().GetResult();

        // Кэшируем строго ключи каталога (whitelist приоритетнее строк БД); ключ без
        // строки → null, как и при пер-ключевом чтении.
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var key in SettingDefinitions.All.Keys)
        {
            values[key] = all.TryGetValue(key, out var v) ? v : null;
        }

        return values;
    }

    private sealed record CacheState(Dictionary<string, string?> Values, DateTimeOffset LoadedAt);
}
