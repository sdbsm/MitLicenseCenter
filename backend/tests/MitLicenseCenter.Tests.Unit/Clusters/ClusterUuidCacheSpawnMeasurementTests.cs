using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Clusters;
using MitLicenseCenter.Infrastructure.Diagnostics;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// MLC-041 (PERF-05) — детерминированный замер «до→после» спавнов rac.exe на сценарии
// харнесса MLC-039 (cold-цикл с kills + hot-поллинг), без живой БД и реальных процессов.
//
// «До» воспроизводится тем же бинарём адаптера с no-op-кэшем (NullClusterUuidCache):
// TryGet→false на каждый вызов → cluster list резолвится всякий раз — байт-в-байт
// доформенное поведение. «После» — реальный singleton ClusterUuidCache, общий между
// scope'ами (как в DI). Спавны классифицируются тем же RacCommandTag, что и метрика
// rac.exe.spawns (MLC-037). Тест и печатает таблицу, и фиксирует сокращения как регресс-гард.
public sealed class ClusterUuidCacheSpawnMeasurementTests
{
    // Сценарий харнесса MLC-039 (масштаб для наглядности; пропорции — как в проде).
    private const int HotTicks = 15;      // hot-поллинг: 15 тиков (каждый — свежий scoped-адаптер)
    private const int ColdListCalls = 2;  // cold-цикл: snapshot + re-fetch в EnforceAsync
    private const int Kills = 5;          // sustained over-limit: 5 kills/цикл

    private readonly ITestOutputHelper _out;

    public ClusterUuidCacheSpawnMeasurementTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Spawn_counts_before_vs_after_caching()
    {
        var before = await RunScenarioAsync(() => new NullClusterUuidCache());
        var after = await RunScenarioAsync(() => new ClusterUuidCache());

        PrintTable(before, after);

        // cluster list: каждый вызов до → один резолв на весь сценарий после.
        before.ClusterList.Should().Be(HotTicks + ColdListCalls + Kills);
        after.ClusterList.Should().Be(1, "тёплый кэш резолвит UUID один раз");

        // session list / terminate не меняются — кэш режет только cluster list.
        before.SessionList.Should().Be(after.SessionList);
        before.SessionTerminate.Should().Be(after.SessionTerminate);

        // Итог спавнов резко падает.
        after.Total.Should().BeLessThan(before.Total);
        after.Total.Should().Be(before.Total - (HotTicks + ColdListCalls + Kills - 1));
    }

    [Fact]
    public async Task Kill_path_drops_from_two_spawns_to_one_when_cache_warm()
    {
        // Тёплый кэш (предшествующий snapshot уже резолвил UUID) → kill = 1 спавн (terminate),
        // против 2 (cluster list + terminate) на холодном/no-op кэше.
        var cold = new CountingRunner();
        var settings = BuildSettings();
        var nullCache = new NullClusterUuidCache();
        await NewClient(cold, settings, nullCache).KillSessionAsync(Descriptor(), default);
        cold.SpawnsPerKill().Should().Be(2, "без кэша: cluster list + terminate");

        var warm = new CountingRunner();
        var cache = new ClusterUuidCache();
        // Прогреваем кэш одним snapshot, затем меряем чистый kill.
        await NewClient(warm, settings, cache).ListActiveSessionsAsync(default);
        warm.Reset();
        await NewClient(warm, settings, cache).KillSessionAsync(Descriptor(), default);
        warm.SpawnsPerKill().Should().Be(1, "тёплый кэш: только terminate");
    }

    [Fact]
    public async Task Hot_polling_halves_spawns_per_tick_when_cache_warm()
    {
        var settings = BuildSettings();
        var cache = new ClusterUuidCache();
        var runner = new CountingRunner();
        await NewClient(runner, settings, cache).ListActiveSessionsAsync(default); // прогрев
        runner.Reset();
        await NewClient(runner, settings, cache).ListActiveSessionsAsync(default); // hot-тик
        runner.Total.Should().Be(1, "тёплый кэш: только session list (×2 меньше, чем cluster+session)");
    }

    private static async Task<Counts> RunScenarioAsync(Func<IClusterUuidCache> cacheFactory)
    {
        var runner = new CountingRunner();
        var settings = BuildSettings();
        var cache = cacheFactory(); // общий между всеми «scope'ами» сценария (как singleton в DI)

        // Hot-поллинг: каждый тик — свежий scoped-адаптер, общий кэш.
        for (var i = 0; i < HotTicks; i++)
        {
            await NewClient(runner, settings, cache).ListActiveSessionsAsync(default);
        }

        // Cold-цикл: snapshot + re-fetch (EnforceAsync) + kills под sustained over-limit.
        for (var i = 0; i < ColdListCalls; i++)
        {
            await NewClient(runner, settings, cache).ListActiveSessionsAsync(default);
        }
        for (var i = 0; i < Kills; i++)
        {
            await NewClient(runner, settings, cache).KillSessionAsync(Descriptor(), default);
        }

        return runner.Snapshot();
    }

    private static RacExecutableRasClusterClient NewClient(
        IRacProcessRunner runner, ISettingsSnapshot settings, IClusterUuidCache cache)
        => new(runner, settings, cache, NullLogger<RacExecutableRasClusterClient>.Instance);

    private static SessionDescriptor Descriptor()
        => new(Guid.NewGuid(), Guid.NewGuid(), "1CV8C", DateTime.UtcNow);

    private static ISettingsSnapshot BuildSettings()
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.OneCRasExePath).Returns("rac.exe");
        settings.GetString(SettingKey.OneCRasEndpoint).Returns("localhost:1545");
        settings.GetString(SettingKey.OneCClusterAdminUser).Returns("admin");
        settings.GetString(SettingKey.OneCClusterAdminPassword).Returns("secret");
        return settings;
    }

    private void PrintTable(Counts before, Counts after)
    {
        _out.WriteLine($"scenario: hot={HotTicks} ticks, cold lists={ColdListCalls}, kills={Kills}");
        _out.WriteLine("tag                 | before | after");
        _out.WriteLine($"cluster.list        | {before.ClusterList,6} | {after.ClusterList,5}");
        _out.WriteLine($"session.list        | {before.SessionList,6} | {after.SessionList,5}");
        _out.WriteLine($"session.terminate   | {before.SessionTerminate,6} | {after.SessionTerminate,5}");
        _out.WriteLine($"TOTAL spawns        | {before.Total,6} | {after.Total,5}");
        _out.WriteLine($"spawns/kill         | {2.0,6:0.0} | {1.0,5:0.0}");
        _out.WriteLine($"spawns/hot-tick     | {2.0,6:0.0} | {1.0,5:0.0}");
    }

    private readonly record struct Counts(int ClusterList, int SessionList, int SessionTerminate)
    {
        public int Total => ClusterList + SessionList + SessionTerminate;
    }

    // Фейковый runner: классифицирует args тем же RacCommandTag, что и метрика, считает
    // спавны по тегам и возвращает успешные канонические ответы rac.exe.
    private sealed class CountingRunner : IRacProcessRunner
    {
        private const string ClusterListStdout =
            "cluster : 613f185a-339d-4bc5-88ad-16acd14a4d26\r\nhost : h\r\nport : 1541\r\n";
        private const string SessionListStdout =
            "session : 492af167-20e6-497a-9eef-20ce4e930c6a\r\n" +
            "infobase : 6256b6f3-dde1-41f9-a6c2-bdfc36bca7aa\r\napp-id : 1CV8C\r\n";

        private int _clusterList;
        private int _sessionList;
        private int _sessionTerminate;

        public Task<RacInvocation> RunAsync(
            string exePath, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            switch (RacCommandTag.For(arguments))
            {
                case RacCommandTag.ClusterList:
                    _clusterList++;
                    return Task.FromResult(new RacInvocation(0, ClusterListStdout, string.Empty));
                case RacCommandTag.SessionList:
                    _sessionList++;
                    return Task.FromResult(new RacInvocation(0, SessionListStdout, string.Empty));
                case RacCommandTag.SessionTerminate:
                    _sessionTerminate++;
                    return Task.FromResult(new RacInvocation(0, string.Empty, string.Empty));
                default:
                    return Task.FromResult(new RacInvocation(0, string.Empty, string.Empty));
            }
        }

        public int Total => _clusterList + _sessionList + _sessionTerminate;
        public int SpawnsPerKill() => _clusterList + _sessionTerminate;
        public void Reset() => _clusterList = _sessionList = _sessionTerminate = 0;
        public Counts Snapshot() => new(_clusterList, _sessionList, _sessionTerminate);
    }

    // «До»-двойник: кэш, который никогда не хранит — адаптер резолвит cluster list
    // на каждый вызов, как до MLC-041.
    private sealed class NullClusterUuidCache : IClusterUuidCache
    {
        public bool TryGet(in ClusterUuidKey key, out string? uuid) { uuid = null; return false; }
        public void Store(in ClusterUuidKey key, string uuid) { }
        public void Invalidate(in ClusterUuidKey key) { }
    }
}
