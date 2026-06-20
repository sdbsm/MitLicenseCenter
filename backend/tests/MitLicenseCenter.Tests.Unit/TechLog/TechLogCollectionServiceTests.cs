using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.TechLog;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.TechLog;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-230 (ADR-57/58): сервис жизненного цикла сбора ТЖ. Установка/снятие через TimeProvider,
// идемпотентность, сторож на старте. БД — EF InMemory; logcfg — FakeLogcfgStore (без ФС/живой 1С).
public sealed class TechLogCollectionServiceTests
{
    private static readonly DateTime Start = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task InstallAsync_writes_logcfg_persists_active_and_audits()
    {
        using var fx = new Fixture();

        var result = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, "mitpro", CancellationToken.None);

        result.Outcome.Should().Be(TechLogStartOutcome.Started);
        fx.Service.HasActiveCollection.Should().BeTrue();
        fx.Store.WriteCalls.Should().Be(1);
        fx.Store.Current.Should().Contain("p:processName").And.Contain("mitpro");

        var row = await fx.QueryAsync(db => db.TechLogCollections.SingleAsync());
        row.Status.Should().Be(TechLogCollectionStatus.Active);
        row.Scenario.Should().Be("Locks");
        row.InfobaseProcessName.Should().Be("mitpro");
        row.StartedAtUtc.Should().Be(Start);

        fx.Audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.TechLogCollectionStarted);
    }

    [Fact]
    public async Task InstallAsync_when_already_active_returns_AlreadyActive_without_second_write()
    {
        using var fx = new Fixture();
        var first = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        var second = await fx.Service.InstallAsync("admin2", TechLogScenario.SlowQueries, null, CancellationToken.None);

        second.Outcome.Should().Be(TechLogStartOutcome.AlreadyActive);
        second.CollectionId.Should().Be(first.CollectionId);
        fx.Store.WriteCalls.Should().Be(1, "повторная установка не переписывает logcfg");
        (await fx.QueryAsync(db => db.TechLogCollections.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task InstallAsync_without_write_access_returns_grant_command_and_no_state_change()
    {
        using var fx = new Fixture();
        fx.Store.Writable = false;

        var result = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, "mitpro", CancellationToken.None);

        result.Outcome.Should().Be(TechLogStartOutcome.NoWriteAccess);
        result.GrantCommand.Should().Contain("icacls").And.Contain("NT SERVICE\\MitLicenseCenter:(M)");
        fx.Service.HasActiveCollection.Should().BeFalse();
        fx.Store.WriteCalls.Should().Be(0);
        (await fx.QueryAsync(db => db.TechLogCollections.CountAsync())).Should().Be(0);
        fx.Audit.Entries.Should().BeEmpty("аудит — только при фактическом успехе");
    }

    [Fact]
    public async Task InstallAsync_when_root_not_found_returns_RootNotFound()
    {
        using var fx = new Fixture();
        fx.Store.RootFound = false;

        var result = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        result.Outcome.Should().Be(TechLogStartOutcome.RootNotFound);
        fx.Store.WriteCalls.Should().Be(0);
    }

    [Fact]
    public async Task RemoveAsync_restores_original_marks_stopped_and_audits()
    {
        using var fx = new Fixture();
        fx.Store.Current = "<config><!-- original --></config>"; // исходный конфиг для бэкапа
        var start = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);
        fx.Clock.Advance(TimeSpan.FromMinutes(5));

        var outcome = await fx.Service.RemoveAsync(start.CollectionId, TechLogCollectionStopReason.Manual, CancellationToken.None);

        outcome.Should().Be(TechLogStopOutcome.Stopped);
        fx.Service.HasActiveCollection.Should().BeFalse();
        fx.Store.RestoreCalls.Should().Be(1);
        fx.Store.Current.Should().Be("<config><!-- original --></config>", "исходный logcfg восстановлен из бэкапа");

        var row = await fx.QueryAsync(db => db.TechLogCollections.SingleAsync());
        row.Status.Should().Be(TechLogCollectionStatus.Stopped);
        row.StopReason.Should().Be(TechLogCollectionStopReason.Manual);
        row.StoppedAtUtc.Should().Be(Start.AddMinutes(5));
        fx.Audit.Entries.Should().Contain(e => e.Action == AuditActionType.TechLogCollectionStopped);
    }

    [Fact]
    public async Task RemoveAsync_with_unknown_id_returns_NotActive()
    {
        using var fx = new Fixture();
        await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        var outcome = await fx.Service.RemoveAsync(Guid.NewGuid(), TechLogCollectionStopReason.Manual, CancellationToken.None);

        outcome.Should().Be(TechLogStopOutcome.NotActive);
        fx.Service.HasActiveCollection.Should().BeTrue();
        fx.Store.RestoreCalls.Should().Be(0);
    }

    [Fact]
    public async Task Install_then_remove_is_idempotent_round_trip()
    {
        using var fx = new Fixture();
        var start = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);
        await fx.Service.RemoveAsync(start.CollectionId, TechLogCollectionStopReason.Manual, CancellationToken.None);

        // Повторная установка после снятия — нормальный новый цикл, состояние не сломано.
        var again = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);
        again.Outcome.Should().Be(TechLogStartOutcome.Started);
        again.CollectionId.Should().NotBe(start.CollectionId);
    }

    [Fact]
    public async Task ReconcileOnStartup_force_restores_forgotten_managed_config_without_active()
    {
        using var fx = new Fixture();
        // В conf лежит НАШ конфиг (по маркеру), но активного дела в БД нет (краш ОС).
        fx.Store.Current = $"<!-- {LogcfgBuilder.Marker} --><config/>";

        await fx.Service.ReconcileOnStartupAsync(CancellationToken.None);

        fx.Store.RestoreCalls.Should().Be(1);
        fx.Store.Current.Should().BeNull("«забытый» конфиг снят (бэкапа не было → файл удалён)");
        fx.Audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.TechLogConfigForceRestored);
    }

    [Fact]
    public async Task ReconcileOnStartup_leaves_foreign_config_untouched()
    {
        using var fx = new Fixture();
        fx.Store.Current = "<config><!-- чужой ТЖ оператора --></config>";

        await fx.Service.ReconcileOnStartupAsync(CancellationToken.None);

        fx.Store.RestoreCalls.Should().Be(0, "чужой конфиг сторож не трогает");
        fx.Store.Current.Should().Contain("чужой ТЖ оператора");
        fx.Audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcileOnStartup_keeps_managed_config_when_active_collection_exists()
    {
        using var fx = new Fixture();
        // Наш конфиг + активное дело в БД (штатно работающий сбор, процесс перезапущен) → не снимаем.
        await fx.QueryAsync(async db =>
        {
            db.TechLogCollections.Add(new TechLogCollection
            {
                Id = Guid.NewGuid(),
                Status = TechLogCollectionStatus.Active,
                StartedAtUtc = Start.AddMinutes(-1),
                Scenario = "Locks",
                CollectionDirectory = @"C:\techlog",
                ConfigMarker = LogcfgBuilder.Marker,
            });
            await db.SaveChangesAsync();
            return 0;
        });
        fx.Store.Current = $"<!-- {LogcfgBuilder.Marker} --><config/>";

        await fx.Service.ReconcileOnStartupAsync(CancellationToken.None);

        fx.Store.RestoreCalls.Should().Be(0, "при активном деле конфиг остаётся (orphan-recovery — MLC-231)");
        fx.Audit.Entries.Should().BeEmpty();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _sp;

        public Fixture()
        {
            var services = new ServiceCollection();
            // Имя БД фиксируем в локальной (не внутри лямбды) — иначе Guid генерируется на КАЖДОЕ
            // создание контекста, и scope'ы получают РАЗНЫЕ in-memory БД (данные не разделяются).
            var dbName = $"techlog-{Guid.NewGuid():N}";
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
            Audit = new CapturingAuditLogger();
            services.AddScoped<IAuditLogger>(_ => Audit);
            _sp = services.BuildServiceProvider();

            Store = new FakeLogcfgStore();
            Settings = Substitute.For<ISettingsSnapshot>(); // GetString/GetInt → null → дефолты сервиса
            Clock = new MutableClock(new DateTimeOffset(Start, TimeSpan.Zero));

            Service = new TechLogCollectionService(
                _sp.GetRequiredService<IServiceScopeFactory>(),
                new LogcfgBuilder(),
                Store,
                Settings,
                Clock,
                NullLogger<TechLogCollectionService>.Instance);
        }

        public TechLogCollectionService Service { get; }
        public FakeLogcfgStore Store { get; }
        public ISettingsSnapshot Settings { get; }
        public MutableClock Clock { get; }
        public CapturingAuditLogger Audit { get; }

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

    // Локальный CapturingAuditLogger: Enlist складывает запись синхронно (тест-дубль не моделирует
    // отложенный SaveChanges; для проверок состава/числа достаточно).
    private sealed class CapturingAuditLogger : IAuditLogger
    {
        public List<(AuditActionType Action, string Initiator, string Description)> Entries { get; } = [];

        public Task LogAsync(AuditActionType action, string initiator, string description,
            Guid? tenantId = null, AuditReason? reason = null, CancellationToken ct = default)
        {
            Entries.Add((action, initiator, description));
            return Task.CompletedTask;
        }

        public void Enlist(AuditActionType action, string initiator, string description,
            Guid? tenantId = null, AuditReason? reason = null)
            => Entries.Add((action, initiator, description));
    }

    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset _now;
        public MutableClock(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
