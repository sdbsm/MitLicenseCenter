using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Performance;
using MitLicenseCenter.Infrastructure.Performance.Testing;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Performance;

// MLC-070 (ADR-26, Фаза 4): сервис записи по требованию. Старт/стоп/авто-стоп через TimeProvider
// (детерминированно), восстановление осиротевшей записи в Interrupted, проводка сэмпла. БД —
// EF InMemory (FK-cascade проверяет отдельный SQLite-тест PerfRecordingPersistenceTests).
public sealed class PerfRecordingServiceTests
{
    private static readonly DateTime Start = new(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StartAsync_persists_active_recording_and_marks_active()
    {
        using var fx = new Fixture();

        var result = await fx.Service.StartAsync("admin", CancellationToken.None);

        result.Outcome.Should().Be(PerfRecordingStartOutcome.Started);
        fx.Service.HasActiveRecording.Should().BeTrue();

        var row = await fx.QueryAsync(db => db.PerfRecordings.SingleAsync());
        row.Id.Should().Be(result.RecordingId);
        row.Status.Should().Be(PerfRecordingStatus.Active);
        row.StartedBy.Should().Be("admin");
        row.StartedAtUtc.Should().Be(Start);
        row.StoppedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_when_already_active_returns_AlreadyActive_with_current_id()
    {
        using var fx = new Fixture();
        var first = await fx.Service.StartAsync("admin", CancellationToken.None);

        var second = await fx.Service.StartAsync("admin2", CancellationToken.None);

        second.Outcome.Should().Be(PerfRecordingStartOutcome.AlreadyActive);
        second.RecordingId.Should().Be(first.RecordingId);
        (await fx.QueryAsync(db => db.PerfRecordings.CountAsync())).Should().Be(1, "одна запись за раз");
    }

    [Fact]
    public async Task StopAsync_finalizes_active_recording_as_stopped_manual()
    {
        using var fx = new Fixture();
        var start = await fx.Service.StartAsync("admin", CancellationToken.None);
        fx.Clock.Advance(TimeSpan.FromSeconds(30));

        var outcome = await fx.Service.StopAsync(start.RecordingId, CancellationToken.None);

        outcome.Should().Be(PerfRecordingStopOutcome.Stopped);
        fx.Service.HasActiveRecording.Should().BeFalse();

        var row = await fx.QueryAsync(db => db.PerfRecordings.SingleAsync());
        row.Status.Should().Be(PerfRecordingStatus.Stopped);
        row.StopReason.Should().Be(PerfRecordingStopReason.Manual);
        row.StoppedAtUtc.Should().Be(Start.AddSeconds(30));
    }

    [Fact]
    public async Task StopAsync_with_unknown_id_returns_NotActive()
    {
        using var fx = new Fixture();
        await fx.Service.StartAsync("admin", CancellationToken.None);

        var outcome = await fx.Service.StopAsync(Guid.NewGuid(), CancellationToken.None);

        outcome.Should().Be(PerfRecordingStopOutcome.NotActive);
        fx.Service.HasActiveRecording.Should().BeTrue("чужой id не останавливает активную запись");
    }

    [Fact]
    public async Task SampleOnceAsync_persists_a_sample_with_host_attribution()
    {
        using var fx = new Fixture();
        var start = await fx.Service.StartAsync("admin", CancellationToken.None);

        await fx.Service.SampleOnceAsync(CancellationToken.None);

        var sample = await fx.QueryAsync(db => db.PerfRecordingSamples.SingleAsync());
        sample.RecordingId.Should().Be(start.RecordingId);
        // Стаб host-пробы отдаёт CPU 12% и семьи OneC/Mssql — попадают в плоские колонки и JSON.
        sample.CpuPercent.Should().Be(12);
        sample.ProcessGroupsJson.Should().Contain("OneC").And.Contain("Mssql");
        // rac не настроен (стаб-кластер отдаёт пусто) → виновников 1С нет (null), не ложный нуль.
        sample.OneCLoadJson.Should().BeNull();
    }

    [Fact]
    public async Task SampleOnceAsync_is_noop_without_active_recording()
    {
        using var fx = new Fixture();

        await fx.Service.SampleOnceAsync(CancellationToken.None);

        (await fx.QueryAsync(db => db.PerfRecordingSamples.CountAsync())).Should().Be(0);
        fx.Host.CaptureCalls.Should().Be(0, "без активной записи источники не читаются");
    }

    [Fact]
    public async Task SampleOnceAsync_auto_stops_on_sample_limit()
    {
        using var fx = new Fixture();
        // Минимальный валидный лимит сэмплов = 10 (whitelist-минимум; меньшие значения клампятся).
        fx.Settings.GetInt(SettingKey.PerformanceRecordingMaxSamples).Returns(10);
        var start = await fx.Service.StartAsync("admin", CancellationToken.None);

        for (var i = 0; i < 9; i++)
        {
            await fx.Service.SampleOnceAsync(CancellationToken.None);
            fx.Service.HasActiveRecording.Should().BeTrue($"после {i + 1} сэмплов лимит ещё не достигнут");
        }

        await fx.Service.SampleOnceAsync(CancellationToken.None); // 10-й — авто-стоп

        fx.Service.HasActiveRecording.Should().BeFalse();
        var row = await fx.QueryAsync(db => db.PerfRecordings.SingleAsync(r => r.Id == start.RecordingId));
        row.Status.Should().Be(PerfRecordingStatus.Stopped);
        row.StopReason.Should().Be(PerfRecordingStopReason.SampleLimit);
        (await fx.QueryAsync(db => db.PerfRecordingSamples.CountAsync())).Should().Be(10, "последний сэмпл записан до стопа");
    }

    [Fact]
    public async Task SampleOnceAsync_auto_stops_on_time_limit()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.PerformanceRecordingMaxDurationMinutes).Returns(1); // 1 минута
        var start = await fx.Service.StartAsync("admin", CancellationToken.None);

        await fx.Service.SampleOnceAsync(CancellationToken.None); // T0 — в пределах лимита
        fx.Service.HasActiveRecording.Should().BeTrue();

        fx.Clock.Advance(TimeSpan.FromMinutes(2)); // вышли за лимит
        await fx.Service.SampleOnceAsync(CancellationToken.None);

        fx.Service.HasActiveRecording.Should().BeFalse();
        var row = await fx.QueryAsync(db => db.PerfRecordings.SingleAsync(r => r.Id == start.RecordingId));
        row.Status.Should().Be(PerfRecordingStatus.Stopped);
        row.StopReason.Should().Be(PerfRecordingStopReason.TimeLimit);
        row.StoppedAtUtc.Should().Be(Start.AddMinutes(2));
    }

    [Fact]
    public async Task RecoverInterruptedAsync_marks_orphaned_active_recordings_interrupted()
    {
        using var fx = new Fixture();
        var orphanId = Guid.NewGuid();
        await fx.QueryAsync(async db =>
        {
            db.PerfRecordings.Add(new PerfRecording
            {
                Id = orphanId,
                StartedAtUtc = Start.AddMinutes(-5),
                Status = PerfRecordingStatus.Active,
                StartedBy = "admin",
            });
            await db.SaveChangesAsync();
            return 0;
        });

        await fx.Service.RecoverInterruptedAsync(CancellationToken.None);

        var row = await fx.QueryAsync(db => db.PerfRecordings.SingleAsync(r => r.Id == orphanId));
        row.Status.Should().Be(PerfRecordingStatus.Interrupted);
        row.StopReason.Should().BeNull("Interrupted не «остановлена по причине», а оборвана рестартом");
        row.StoppedAtUtc.Should().Be(Start);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _sp;

        public Fixture()
        {
            var services = new ServiceCollection();
            var dbName = $"perfrec-{Guid.NewGuid():N}";
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));

            Cluster = Substitute.For<IClusterClient>();
            Cluster.ListSessionLoadsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<OneCSessionLoad>>([]));
            Cluster.ListProcessesAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<OneCProcessLoad>>([]));
            services.AddScoped(_ => Cluster);

            _sp = services.BuildServiceProvider();

            Host = new StubHostMetricsProbe();
            Sql = new StubSqlPerformanceProbe();
            Settings = Substitute.For<ISettingsSnapshot>(); // GetInt → null → дефолты
            Clock = new MutableClock(new DateTimeOffset(Start, TimeSpan.Zero));

            Service = new PerfRecordingService(
                _sp.GetRequiredService<IServiceScopeFactory>(),
                Host,
                Sql,
                Settings,
                Clock,
                NullLogger<PerfRecordingService>.Instance);
        }

        public PerfRecordingService Service { get; }
        public IClusterClient Cluster { get; }
        public StubHostMetricsProbe Host { get; }
        public StubSqlPerformanceProbe Sql { get; }
        public ISettingsSnapshot Settings { get; }
        public MutableClock Clock { get; }

        public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> f)
        {
            using var scope = _sp.CreateScope();
            return await f(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public void Dispose()
        {
            Service.Dispose();
            _sp.Dispose();
        }
    }

    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset _now;
        public MutableClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
