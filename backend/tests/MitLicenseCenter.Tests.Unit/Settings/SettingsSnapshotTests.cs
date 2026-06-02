using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Infrastructure.Settings;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Settings;

// MLC-010: hot-path-кэш читает БД через bulk GetAllAsync вне лока, single-flight.
// Проверяем семантику TTL, инвалидацию и отсутствие двойной загрузки.
public sealed class SettingsSnapshotTests
{
    private static (SettingsSnapshot Snapshot, FakeStore Store, MutableClock Clock) Make(
        Dictionary<string, string?> values)
    {
        var store = new FakeStore(values);
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsStore>(store);
        var provider = services.BuildServiceProvider();
        var clock = new MutableClock(DateTimeOffset.UnixEpoch);
        var snapshot = new SettingsSnapshot(provider.GetRequiredService<IServiceScopeFactory>(), clock);
        return (snapshot, store, clock);
    }

    [Fact]
    public void GetString_returns_value_including_decrypted_secret()
    {
        // Store отдаёт уже расшифрованный секрет (как делает реальный GetAllAsync).
        var (snapshot, _, _) = Make(new()
        {
            ["OneC.Cluster.AdminUser"] = "admin",
            ["OneC.Cluster.AdminPassword"] = "hunter2",
        });

        snapshot.GetString("OneC.Cluster.AdminUser").Should().Be("admin");
        snapshot.GetString("OneC.Cluster.AdminPassword").Should().Be("hunter2");
    }

    [Fact]
    public void GetInt_parses_cached_value()
    {
        var (snapshot, _, _) = Make(new() { ["Polling.HotIntervalSeconds"] = "7" });

        snapshot.GetInt("Polling.HotIntervalSeconds").Should().Be(7);
    }

    [Fact]
    public void Unknown_key_returns_null_without_extra_load()
    {
        var (snapshot, store, _) = Make(new() { ["OneC.Cluster.AdminUser"] = "admin" });

        snapshot.GetString("Not.In.Catalog").Should().BeNull();
        store.LoadCount.Should().Be(1);
    }

    [Fact]
    public void Repeated_reads_within_TTL_load_once()
    {
        var (snapshot, store, _) = Make(new() { ["OneC.Cluster.AdminUser"] = "admin" });

        for (var i = 0; i < 5; i++)
        {
            snapshot.GetString("OneC.Cluster.AdminUser").Should().Be("admin");
        }

        store.LoadCount.Should().Be(1);
    }

    [Fact]
    public void Read_after_TTL_reloads()
    {
        var (snapshot, store, clock) = Make(new() { ["OneC.Cluster.AdminUser"] = "admin" });

        snapshot.GetString("OneC.Cluster.AdminUser");
        clock.Advance(TimeSpan.FromSeconds(31));
        snapshot.GetString("OneC.Cluster.AdminUser");

        store.LoadCount.Should().Be(2);
    }

    [Fact]
    public void Reads_just_under_TTL_do_not_reload()
    {
        var (snapshot, store, clock) = Make(new() { ["OneC.Cluster.AdminUser"] = "admin" });

        snapshot.GetString("OneC.Cluster.AdminUser");
        clock.Advance(TimeSpan.FromSeconds(29));
        snapshot.GetString("OneC.Cluster.AdminUser");

        store.LoadCount.Should().Be(1);
    }

    [Fact]
    public void Invalidate_forces_reload()
    {
        var (snapshot, store, _) = Make(new() { ["OneC.Cluster.AdminUser"] = "admin" });

        snapshot.GetString("OneC.Cluster.AdminUser");
        store.LoadCount.Should().Be(1);

        snapshot.Invalidate();
        snapshot.GetString("OneC.Cluster.AdminUser");

        // Перезагрузка форсирована даже в пределах TTL (clock не двигали).
        store.LoadCount.Should().Be(2);
    }

    [Fact]
    public void Invalidate_picks_up_new_values()
    {
        var (snapshot, store, _) = Make(new() { ["OneC.Cluster.AdminUser"] = "admin" });

        snapshot.GetString("OneC.Cluster.AdminUser").Should().Be("admin");
        store.Values["OneC.Cluster.AdminUser"] = "root";
        snapshot.Invalidate();

        snapshot.GetString("OneC.Cluster.AdminUser").Should().Be("root");
    }

    [Fact]
    public async Task Concurrent_cold_readers_load_only_once()
    {
        var (snapshot, store, _) = Make(new() { ["OneC.Cluster.AdminUser"] = "admin" });

        // Удерживаем первую загрузку внутри store, пока остальные читатели набегают:
        // single-flight должен схлопнуть их в один Task → одна загрузка БД.
        store.BlockInsideLoad = true;

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => snapshot.GetString("OneC.Cluster.AdminUser")))
            .ToArray();

        // Дожидаемся, пока загрузчик окажется внутри GetAllAsync, даём остальным
        // дойти до критической секции и отпускаем.
        store.Entered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
        await Task.Delay(50);
        store.Release.Set();

        var results = await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.Should().Be("admin"));
        store.LoadCount.Should().Be(1);
    }

    private sealed class FakeStore : ISettingsStore
    {
        public ConcurrentDictionary<string, string?> Values { get; }
        private int _loadCount;
        public int LoadCount => Volatile.Read(ref _loadCount);

        public bool BlockInsideLoad { get; set; }
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);

        public FakeStore(Dictionary<string, string?> values)
        {
            Values = new ConcurrentDictionary<string, string?>(values, StringComparer.Ordinal);
        }

        public Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _loadCount);
            if (BlockInsideLoad)
            {
                Entered.Set();
                Release.Wait(ct);
            }

            IReadOnlyDictionary<string, string?> snapshot =
                new Dictionary<string, string?>(Values, StringComparer.Ordinal);
            return Task.FromResult(snapshot);
        }

        public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<int?> GetIntAsync(string key, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task SetAsync(string key, string? value, bool isSecret, string updatedBy, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SettingDescriptor>> ListAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset _now;
        public MutableClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
