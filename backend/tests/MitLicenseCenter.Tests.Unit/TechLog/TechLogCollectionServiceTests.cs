using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Application.TechLog;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
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

        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.Status.Should().Be(InvestigationStatus.Collecting);
        row.Scenario.Should().Be(InvestigationScenario.Locks);
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
        (await fx.QueryAsync(db => db.Investigations.CountAsync())).Should().Be(1);
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
        (await fx.QueryAsync(db => db.Investigations.CountAsync())).Should().Be(0);
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

        var outcome = await fx.Service.RemoveAsync(start.CollectionId, InvestigationStopReason.Manual, CancellationToken.None);

        outcome.Should().Be(TechLogStopOutcome.Stopped);
        fx.Service.HasActiveCollection.Should().BeFalse();
        fx.Store.RestoreCalls.Should().Be(1);
        fx.Store.Current.Should().Be("<config><!-- original --></config>", "исходный logcfg восстановлен из бэкапа");

        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.Status.Should().Be(InvestigationStatus.Completed);
        row.StopReason.Should().Be(InvestigationStopReason.Manual);
        row.StoppedAtUtc.Should().Be(Start.AddMinutes(5));
        fx.Audit.Entries.Should().Contain(e => e.Action == AuditActionType.TechLogCollectionStopped);
    }

    [Fact]
    public async Task RemoveAsync_with_unknown_id_returns_NotActive()
    {
        using var fx = new Fixture();
        await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        var outcome = await fx.Service.RemoveAsync(Guid.NewGuid(), InvestigationStopReason.Manual, CancellationToken.None);

        outcome.Should().Be(TechLogStopOutcome.NotActive);
        fx.Service.HasActiveCollection.Should().BeTrue();
        fx.Store.RestoreCalls.Should().Be(0);
    }

    [Fact]
    public async Task Install_then_remove_is_idempotent_round_trip()
    {
        using var fx = new Fixture();
        var start = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);
        await fx.Service.RemoveAsync(start.CollectionId, InvestigationStopReason.Manual, CancellationToken.None);

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
            db.Investigations.Add(new Investigation
            {
                Id = Guid.NewGuid(),
                Status = InvestigationStatus.Collecting,
                StartedAtUtc = Start.AddMinutes(-1),
                Scenario = InvestigationScenario.Locks,
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

    // --- MLC-231: безопасный сбор (окно/диск/single-active по БД/orphan-recovery) ---

    [Fact]
    public async Task InstallAsync_when_active_row_in_db_but_inmemory_lost_returns_AlreadyActive()
    {
        using var fx = new Fixture();
        // Активное дело в БД, но in-memory _active пуст (как после рестарта процесса до orphan-recovery).
        var existingId = Guid.NewGuid();
        await fx.QueryAsync(async db =>
        {
            db.Investigations.Add(new Investigation
            {
                Id = existingId,
                Status = InvestigationStatus.Collecting,
                StartedAtUtc = Start.AddMinutes(-1),
                Scenario = InvestigationScenario.Locks,
                CollectionDirectory = @"C:\techlog",
                ConfigMarker = LogcfgBuilder.Marker,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var result = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        result.Outcome.Should().Be(TechLogStartOutcome.AlreadyActive);
        result.CollectionId.Should().Be(existingId);
        fx.Store.WriteCalls.Should().Be(0, "second logcfg НЕ пишется при активном деле в БД");
    }

    [Fact]
    public async Task InstallAsync_when_free_space_below_threshold_returns_InsufficientDiskSpace()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.TechLogMinFreeDiskMb).Returns(1024);
        fx.Store.FreeSpaceBytes = 100L * 1024 * 1024; // 100 МБ < порога 1024 МБ

        var result = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, "mitpro", CancellationToken.None);

        result.Outcome.Should().Be(TechLogStartOutcome.InsufficientDiskSpace);
        result.Issue.Should().Contain("100").And.Contain("1024");
        fx.Store.WriteCalls.Should().Be(0);
        fx.Service.HasActiveCollection.Should().BeFalse();
        (await fx.QueryAsync(db => db.Investigations.CountAsync())).Should().Be(0);
        fx.Audit.Entries.Should().BeEmpty("аудит — только при фактическом успехе");
    }

    [Fact]
    public async Task InstallAsync_when_free_space_probe_null_starts_anyway()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.TechLogMinFreeDiskMb).Returns(1024);
        fx.Store.FreeSpaceBytes = null; // том не определить → проверка невозможна, не блокируем

        var result = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        result.Outcome.Should().Be(TechLogStartOutcome.Started);
    }

    // --- MLC-247 A2: проба прав аккаунта агента 1С на каталог сбора (паттерн RAS-healing) ---

    [Fact]
    public async Task InstallAsync_when_agent_account_set_and_no_access_returns_grant_command_and_no_case()
    {
        using var fx = new Fixture();
        fx.Settings.GetString(SettingKey.TechLogCollectionAgentAccount).Returns(".\\mitpro");
        fx.Store.AgentAclResult = new DirectoryAclProbeResult(
            HasAccess: false, Determined: true,
            GrantCommand: "icacls \"C:\\techlog\" /grant \".\\mitpro:(OI)(CI)(M)\" /T",
            Issue: "нет прав записи у агента");

        var result = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, "mitpro", CancellationToken.None);

        result.Outcome.Should().Be(TechLogStartOutcome.AgentNoCollectionAccess);
        result.GrantCommand.Should().Contain("icacls").And.Contain("mitpro").And.Contain("(OI)(CI)(M)");
        fx.Service.HasActiveCollection.Should().BeFalse();
        fx.Store.WriteCalls.Should().Be(0, "сбор не стартует без прав агента — иначе «пустые дела»");
        (await fx.QueryAsync(db => db.Investigations.CountAsync())).Should().Be(0);
        fx.Audit.Entries.Should().BeEmpty("аудит — только при фактическом успехе");
        fx.Store.AgentAclProbedAccount.Should().Be(".\\mitpro");
    }

    [Fact]
    public async Task InstallAsync_when_agent_account_set_and_has_access_starts()
    {
        using var fx = new Fixture();
        fx.Settings.GetString(SettingKey.TechLogCollectionAgentAccount).Returns(".\\mitpro");
        fx.Store.AgentAclResult = new DirectoryAclProbeResult(
            HasAccess: true, Determined: true, GrantCommand: null, Issue: null);

        var result = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, "mitpro", CancellationToken.None);

        result.Outcome.Should().Be(TechLogStartOutcome.Started);
        fx.Store.WriteCalls.Should().Be(1);
        fx.Service.HasActiveCollection.Should().BeTrue();
    }

    [Fact]
    public async Task InstallAsync_when_agent_account_empty_starts_without_blocking()
    {
        using var fx = new Fixture();
        // Аккаунт не задан (дефолт) → панель не знает аккаунт, не блокирует (предупреждение в лог).
        // Проба прав даже не вызывается.
        fx.Store.AgentAclResult = new DirectoryAclProbeResult(
            HasAccess: false, Determined: true, GrantCommand: "icacls ...", Issue: "нет прав");

        var result = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, "mitpro", CancellationToken.None);

        result.Outcome.Should().Be(TechLogStartOutcome.Started, "пустой аккаунт установку не блокирует");
        fx.Store.WriteCalls.Should().Be(1);
        fx.Store.AgentAclProbedAccount.Should().BeNull("при пустом аккаунте проба прав не вызывается");
    }

    [Fact]
    public async Task InstallAsync_when_agent_acl_undetermined_starts_without_blocking()
    {
        using var fx = new Fixture();
        fx.Settings.GetString(SettingKey.TechLogCollectionAgentAccount).Returns(".\\mitpro");
        // «Проверка невозможна» (не-Windows / права через группу) → толерантно НЕ блокируем.
        fx.Store.AgentAclResult = new DirectoryAclProbeResult(
            HasAccess: false, Determined: false, GrantCommand: "icacls ...", Issue: "проверка невозможна");

        var result = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, "mitpro", CancellationToken.None);

        result.Outcome.Should().Be(TechLogStartOutcome.Started,
            "недетерминированная проба прав не блокирует старт (best-effort)");
        fx.Store.WriteCalls.Should().Be(1);
    }

    [Fact]
    public async Task MonitorActiveAsync_auto_stops_on_time_limit()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.TechLogMaxDurationMinutes).Returns(10);
        var start = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        fx.Clock.Advance(TimeSpan.FromMinutes(10)); // достигли окна
        await fx.Service.MonitorActiveAsync(CancellationToken.None);

        fx.Service.HasActiveCollection.Should().BeFalse();
        fx.Store.RestoreCalls.Should().Be(1, "logcfg снят при авто-стопе");
        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.Status.Should().Be(InvestigationStatus.Completed);
        row.StopReason.Should().Be(InvestigationStopReason.TimeLimit);
        row.StoppedAtUtc.Should().Be(Start.AddMinutes(10));
        fx.Audit.Entries.Should().Contain(e => e.Action == AuditActionType.TechLogCollectionStopped);
    }

    [Fact]
    public async Task MonitorActiveAsync_before_time_limit_keeps_collection_active()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.TechLogMaxDurationMinutes).Returns(10);
        await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        fx.Clock.Advance(TimeSpan.FromMinutes(5)); // окно ещё не достигнуто
        await fx.Service.MonitorActiveAsync(CancellationToken.None);

        fx.Service.HasActiveCollection.Should().BeTrue();
        fx.Store.RestoreCalls.Should().Be(0);
    }

    [Fact]
    public async Task MonitorActiveAsync_auto_stops_on_disk_limit()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.TechLogMaxDurationMinutes).Returns(60); // окно не сработает
        fx.Settings.GetInt(SettingKey.TechLogDiskLimitMb).Returns(100);
        await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        fx.Store.DirectorySizeBytes = 150L * 1024 * 1024; // 150 МБ ≥ лимита 100 МБ
        await fx.Service.MonitorActiveAsync(CancellationToken.None);

        fx.Service.HasActiveCollection.Should().BeFalse();
        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.Status.Should().Be(InvestigationStatus.Completed);
        row.StopReason.Should().Be(InvestigationStopReason.DiskLimit);
    }

    [Fact]
    public async Task MonitorActiveAsync_below_disk_limit_keeps_active()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.TechLogMaxDurationMinutes).Returns(60);
        fx.Settings.GetInt(SettingKey.TechLogDiskLimitMb).Returns(100);
        await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        fx.Store.DirectorySizeBytes = 50L * 1024 * 1024; // 50 МБ < лимита
        await fx.Service.MonitorActiveAsync(CancellationToken.None);

        fx.Service.HasActiveCollection.Should().BeTrue();
    }

    [Fact]
    public async Task MonitorActiveAsync_without_active_collection_is_noop()
    {
        using var fx = new Fixture();

        await fx.Service.MonitorActiveAsync(CancellationToken.None);

        fx.Store.RestoreCalls.Should().Be(0);
        fx.Audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RecoverInterruptedAsync_marks_active_rows_interrupted()
    {
        using var fx = new Fixture();
        await fx.QueryAsync(async db =>
        {
            db.Investigations.Add(new Investigation
            {
                Id = Guid.NewGuid(),
                Status = InvestigationStatus.Collecting,
                StartedAtUtc = Start.AddMinutes(-30),
                Scenario = InvestigationScenario.Locks,
                CollectionDirectory = @"C:\techlog",
                ConfigMarker = LogcfgBuilder.Marker,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        await fx.Service.RecoverInterruptedAsync(CancellationToken.None);

        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.Status.Should().Be(InvestigationStatus.Interrupted);
        row.StoppedAtUtc.Should().Be(Start);
        row.StopReason.Should().BeNull("Interrupted — не «остановлено по причине»");
    }

    [Fact]
    public async Task Startup_recover_then_reconcile_marks_interrupted_and_strips_forgotten_config()
    {
        using var fx = new Fixture();
        // Осиротевшее дело в БД + наш logcfg в conf (краш ОС: in-memory стейт потерян).
        await fx.QueryAsync(async db =>
        {
            db.Investigations.Add(new Investigation
            {
                Id = Guid.NewGuid(),
                Status = InvestigationStatus.Collecting,
                StartedAtUtc = Start.AddMinutes(-30),
                Scenario = InvestigationScenario.Locks,
                CollectionDirectory = @"C:\techlog",
                ConfigMarker = LogcfgBuilder.Marker,
            });
            await db.SaveChangesAsync();
            return 0;
        });
        fx.Store.Current = $"<!-- {LogcfgBuilder.Marker} --><config/>";

        // Порядок драйвера: сначала orphan-recovery, ПОТОМ стартовая сверка файла.
        await fx.Service.RecoverInterruptedAsync(CancellationToken.None);
        await fx.Service.ReconcileOnStartupAsync(CancellationToken.None);

        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.Status.Should().Be(InvestigationStatus.Interrupted, "осиротевшее дело помечено прерванным");
        fx.Store.RestoreCalls.Should().Be(1, "после перевода дела в Interrupted активного нет → logcfg снят");
        fx.Store.Current.Should().BeNull("«забытый» конфиг снят (бэкапа не было → файл удалён)");
        fx.Audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.TechLogConfigForceRestored);
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
