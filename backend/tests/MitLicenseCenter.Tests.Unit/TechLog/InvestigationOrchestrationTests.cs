using System.Text;
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

// MLC-238 (этап C): конвейер «Дела» расследования. Снятие сбора (ручное/авто-стоп) проводит дело через
// Analyzing → разбор сырья ТЖ (seam ILogcfgStore.ReadCollectionLines → парсер → анализаторы сценария) →
// Finding'и → Completed + удаление сырья. Ошибка конвейера → Failed (сырьё сохраняется). Снимок
// CollectionConfig наполняется на старте; инвариант изоляции арендатора энфорсится.
//
// БД — EF InMemory (как TechLogCollectionServiceTests); парсер/анализаторы — реальные stateless этапа B;
// сырьё — фиксированные NDJSON-строки из фикстур этапа B (Store.CollectionLines).
public sealed class InvestigationOrchestrationTests
{
    private static readonly DateTime Start = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

    private static string[] FixtureLines(string name)
        => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TechLog", "Fixtures", name), Encoding.UTF8);

    [Fact]
    public async Task Start_populates_collection_config_snapshot()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.TechLogHistoryHours).Returns(2);

        await fx.Service.InstallAsync("admin", TechLogScenario.SlowQueries, "infobase01", CancellationToken.None);

        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.CollectionConfig.Should().NotBeNull();
        var cfg = row.CollectionConfig!;
        cfg.Events.Should().Be("DBMSSQL,SDBL", "набор событий сценария SlowQueries из ILogcfgBuilder.EventsFor");
        cfg.ProcessNameFilter.Should().Be("infobase01");
        cfg.Format.Should().Be("json");
        cfg.HistoryHours.Should().Be(2);
        cfg.LogcfgLocation.Should().NotBeNullOrEmpty();
        cfg.DurationThresholdMicros.Should().Be(1_000_000,
            "MLC-248: дефолтный порог долгих запросов — 1 c, когда оператор не задал его в Мастере");
    }

    [Fact]
    public async Task Start_for_locks_scenario_leaves_duration_threshold_null()
    {
        using var fx = new Fixture();

        await fx.Service.InstallAsync("admin", TechLogScenario.Locks, null, CancellationToken.None);

        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.CollectionConfig!.DurationThresholdMicros.Should().BeNull("блокировки порогом длительности не отбираются");
        row.CollectionConfig.Events.Should().Be("TLOCK,TTIMEOUT,TDEADLOCK,SDBL");
        row.CollectionConfig.ProcessNameFilter.Should().BeNull("весь кластер — фильтра нет");
    }

    [Fact]
    public async Task Manual_remove_runs_pipeline_completes_and_produces_finding()
    {
        using var fx = new Fixture();
        fx.Store.CollectionLines = FixtureLines("dbmssql-slow.ndjson");
        var start = await fx.Service.InstallAsync("admin", TechLogScenario.SlowQueries, "infobase01", CancellationToken.None);

        var outcome = await fx.Service.RemoveAsync(start.CollectionId, InvestigationStopReason.Manual, CancellationToken.None);

        outcome.Should().Be(TechLogStopOutcome.Stopped);
        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.Status.Should().Be(InvestigationStatus.Completed);
        row.StopReason.Should().Be(InvestigationStopReason.Manual);

        var findings = await fx.QueryAsync(db => db.Findings.ToListAsync());
        findings.Should().ContainSingle();
        findings[0].Kind.Should().Be(FindingKind.SlowQueries);
        findings[0].SchemaVersion.Should().Be(1);
        findings[0].ResultJson.Should().NotBeNullOrEmpty();
        // Реальный анализатор нашёл долгие запросы (фикстура содержит запросы ≥ 5 c) → topQueries не пуст.
        findings[0].ResultJson.Should().Contain("topQueries");

        fx.Store.DeleteCalls.Should().Be(1, "сырьё ТЖ удалено после успешного анализа (MLC-237 Q2)");
        fx.Store.DeletedDirectory.Should().Be(row.CollectionDirectory);
    }

    [Fact]
    public async Task Custom_threshold_is_persisted_and_used_by_analysis_pipeline()
    {
        using var fx = new Fixture();
        fx.Store.CollectionLines = FixtureLines("dbmssql-slow.ndjson");

        // Высокий порог 10 c (10 000 000 µs): ни один запрос фикстуры (макс 7.6 c) не пройдёт в топ,
        // но агрегат «похожие» (MLC-248) от порога не зависит → similarGroups не пуст.
        var start = await fx.Service.InstallAsync(
            "admin", TechLogScenario.SlowQueries, "infobase01", CancellationToken.None,
            slowQueryThresholdMicros: 10_000_000);

        // Снимок несёт фактически применённый порог.
        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.CollectionConfig!.DurationThresholdMicros.Should().Be(10_000_000,
            "порог из Мастера сохраняется в снимок CollectionConfig");

        await fx.Service.RemoveAsync(start.CollectionId, InvestigationStopReason.Manual, CancellationToken.None);

        var findings = await fx.QueryAsync(db => db.Findings.ToListAsync());
        var slow = findings.Single(f => f.Kind == FindingKind.SlowQueries);
        // Конвейер применил порог 10 c из снимка (не хардкод): topQueries пуст, агрегат — нет.
        slow.ResultJson.Should().Contain("\"topQueries\":[]",
            "при пороге 10 c ни один запрос фикстуры не попадает в топ — порог взят из снимка дела");
        slow.ResultJson.Should().Contain("_TableX",
            "агрегат similarGroups НЕЗАВИСИМ от порога — похожие запросы всплывают (MLC-248)");
    }

    [Fact]
    public async Task Locks_scenario_produces_managed_locks_finding()
    {
        using var fx = new Fixture();
        fx.Store.CollectionLines = FixtureLines("tlock.ndjson");
        var start = await fx.Service.InstallAsync("admin", TechLogScenario.Locks, "infobase01", CancellationToken.None);

        await fx.Service.RemoveAsync(start.CollectionId, InvestigationStopReason.Manual, CancellationToken.None);

        var findings = await fx.QueryAsync(db => db.Findings.ToListAsync());
        findings.Should().ContainSingle(f => f.Kind == FindingKind.ManagedLocks);
    }

    [Fact]
    public async Task GeneralSlow_scenario_produces_slowqueries_and_exceptions_findings()
    {
        using var fx = new Fixture();
        fx.Store.CollectionLines = FixtureLines("dbmssql-slow.ndjson");
        var start = await fx.Service.InstallAsync("admin", TechLogScenario.GeneralSlow, "infobase01", CancellationToken.None);

        await fx.Service.RemoveAsync(start.CollectionId, InvestigationStopReason.Manual, CancellationToken.None);

        var findings = await fx.QueryAsync(db => db.Findings.ToListAsync());
        findings.Select(f => f.Kind).Should().BeEquivalentTo(
            new[] { FindingKind.SlowQueries, FindingKind.Exceptions },
            "GeneralSlow = долгие запросы + исключения (решение MLC-238)");
    }

    [Fact]
    public async Task Auto_stop_on_time_limit_runs_pipeline_to_completed()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.TechLogMaxDurationMinutes).Returns(10);
        fx.Store.CollectionLines = FixtureLines("excp.ndjson");
        await fx.Service.InstallAsync("admin", TechLogScenario.Exceptions, "infobase01", CancellationToken.None);

        fx.Clock.Advance(TimeSpan.FromMinutes(10));
        await fx.Service.MonitorActiveAsync(CancellationToken.None);

        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.Status.Should().Be(InvestigationStatus.Completed);
        row.StopReason.Should().Be(InvestigationStopReason.TimeLimit);
        (await fx.QueryAsync(db => db.Findings.CountAsync())).Should().Be(1);
        fx.Store.DeleteCalls.Should().Be(1);
    }

    [Fact]
    public async Task Pipeline_failure_marks_failed_keeps_raw_and_does_not_throw()
    {
        using var fx = new Fixture();
        fx.Store.ThrowOnRead = true; // обрыв чтения сырья ТЖ → ошибка конвейера
        var start = await fx.Service.InstallAsync("admin", TechLogScenario.SlowQueries, "infobase01", CancellationToken.None);

        // Сервис НЕ падает (never-throws конвейера).
        var outcome = await fx.Service.RemoveAsync(start.CollectionId, InvestigationStopReason.Manual, CancellationToken.None);

        outcome.Should().Be(TechLogStopOutcome.Stopped, "снятие сбора прошло; упал только анализ");
        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.Status.Should().Be(InvestigationStatus.Failed);
        (await fx.QueryAsync(db => db.Findings.CountAsync())).Should().Be(0);
        fx.Store.DeleteCalls.Should().Be(0, "при Failed сырьё НЕ удаляем — нужно для разбора");
        fx.Store.RestoreCalls.Should().Be(1, "logcfg всё равно восстановлен (главная гарантия — до анализа)");
        fx.Service.HasActiveCollection.Should().BeFalse();
    }

    [Fact]
    public async Task Empty_raw_still_completes_with_finding()
    {
        using var fx = new Fixture();
        fx.Store.CollectionLines = []; // сырья нет (платформа ничего не записала)
        var start = await fx.Service.InstallAsync("admin", TechLogScenario.DbmsLocks, "infobase01", CancellationToken.None);

        await fx.Service.RemoveAsync(start.CollectionId, InvestigationStopReason.Manual, CancellationToken.None);

        var row = await fx.QueryAsync(db => db.Investigations.SingleAsync());
        row.Status.Should().Be(InvestigationStatus.Completed);
        var findings = await fx.QueryAsync(db => db.Findings.ToListAsync());
        findings.Should().ContainSingle(f => f.Kind == FindingKind.DbmsLocks, "пустой результат — всё равно Finding");
    }

    [Fact]
    public void EnsureProcessFilterInvariant_throws_when_infobase_bound_without_filter()
    {
        // Инвариант изоляции арендатора (60_SAFETY №2): дело с InfobaseId обязано нести p:processName.
        // Сервис InfobaseId отдельно не получает (только infobaseProcessName), поэтому проверяем сам
        // доменный инвариант, который оркестрация вызывает после наполнения снимка.
        var bound = new Investigation
        {
            Id = Guid.NewGuid(),
            Scenario = InvestigationScenario.Locks,
            Status = InvestigationStatus.Collecting,
            StartedAtUtc = Start,
            StartedBy = "admin",
            InfobaseId = Guid.NewGuid(),
            CollectionDirectory = @"C:\techlog",
            ConfigMarker = "managed by MitLicenseCenter",
            CollectionConfig = new CollectionConfig { ProcessNameFilter = null },
        };

        var act = () => bound.EnsureProcessFilterInvariant();

        act.Should().Throw<InvalidOperationException>("InfobaseId задан, но фильтр p:processName пуст");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _sp;

        public Fixture()
        {
            var services = new ServiceCollection();
            var dbName = $"techlog-orch-{Guid.NewGuid():N}";
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
            Audit = new CapturingAuditLogger();
            services.AddScoped<IAuditLogger>(_ => Audit);
            _sp = services.BuildServiceProvider();

            Store = new FakeLogcfgStore();
            Settings = Substitute.For<ISettingsSnapshot>();
            Clock = new MutableClock(new DateTimeOffset(Start, TimeSpan.Zero));

            Service = new TechLogCollectionService(
                _sp.GetRequiredService<IServiceScopeFactory>(),
                new LogcfgBuilder(),
                Store,
                new TechLogParser(),
                new LockTreeAnalyzer(),
                new SlowQueryAnalyzer(),
                new ExceptionAnalyzer(),
                new DbmsLockAnalyzer(),
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
