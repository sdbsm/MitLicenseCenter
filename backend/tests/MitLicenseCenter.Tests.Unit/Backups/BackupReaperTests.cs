using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Backups;
using MitLicenseCenter.Infrastructure.Backups.Testing;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;
using Xunit.Sdk;

namespace MitLicenseCenter.Tests.Unit.Backups;

// MLC-123 (BE-19): TTL-reaper зависших Running. RecoverInterruptedAsync закрывает
// осиротевшие Running только на старте процесса; бэкап, зависший пока процесс жив,
// иначе остался бы в Running навсегда, а его in-memory замок-на-базу — удержанным.
// Reaper (каждый тик насоса) закрывает строки старше StuckRunningTimeout=6ч как
// Failed/TimedOut И снимает замок, разблокируя базу. Время — фейковый TimeProvider.
public sealed class BackupReaperTests
{
    private static readonly DateTime Start = new(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan StuckTimeout = TimeSpan.FromHours(6);

    [Fact]
    public async Task Reaps_stale_running_as_failed_timedout_and_audits()
    {
        using var fx = new Fixture();
        // Стартовал давно (8ч назад) → старше потолка 6ч.
        var stuck = Row("acme_db", BackupStatus.Running, Start.AddHours(-9));
        stuck.StartedAtUtc = Start.AddHours(-8);
        await fx.SeedAsync(stuck);

        await fx.Orchestrator.ReapStuckRunningAsync(CancellationToken.None);

        var reaped = await fx.QueryAsync(db => db.DatabaseBackups.SingleAsync(b => b.Id == stuck.Id));
        reaped.Status.Should().Be(BackupStatus.Failed);
        reaped.FailureReason.Should().Be(BackupFailureReason.TimedOut);
        reaped.CompletedAtUtc.Should().Be(Start);
        reaped.ErrorMessage.Should().Contain("лимит времени");

        await fx.WaitUntilAsync(_ => Task.FromResult(fx.Audit.Entries.Count == 1),
            "reaper пишет одну аудит-запись, когда что-то закрыл");
        var entry = fx.Audit.Entries.Single();
        entry.Action.Should().Be(AuditActionType.BackupFailed, "переиспользуем замороженный enum");
        entry.Initiator.Should().Be("System");
        entry.Description.Should().Contain("acme_db");
    }

    [Fact]
    public async Task Fresh_running_row_is_left_untouched()
    {
        using var fx = new Fixture();
        // Стартовал час назад → внутри потолка 6ч, reap не трогает.
        var fresh = Row("acme_db", BackupStatus.Running, Start.AddHours(-1));
        fresh.StartedAtUtc = Start.AddHours(-1);
        await fx.SeedAsync(fresh);

        await fx.Orchestrator.ReapStuckRunningAsync(CancellationToken.None);

        var row = await fx.QueryAsync(db => db.DatabaseBackups.SingleAsync(b => b.Id == fresh.Id));
        row.Status.Should().Be(BackupStatus.Running, "свежий Running не зависший");
        row.FailureReason.Should().Be(BackupFailureReason.None);
        fx.Audit.Entries.Should().BeEmpty("тихий тик не пишет аудит");
    }

    [Fact]
    public async Task Reaping_releases_the_in_memory_lock_so_a_new_backup_can_start()
    {
        using var fx = new Fixture();
        fx.Backup.BackupGate = new TaskCompletionSource();

        // 1) Запрос + тик: бэкап стартует и «виснет» на воротах, держа замок-на-базу acme_db.
        await fx.RequestAsync("acme_db");
        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);
        await fx.WaitUntilAsync(
            db => db.DatabaseBackups.AnyAsync(b => b.Status == BackupStatus.Running),
            "первый бэкап должен перейти в Running");

        // 2) Время прыгает за потолок → строка зависла. Reaper закроет её и СНИМЕТ замок.
        fx.Clock.Advance(StuckTimeout + TimeSpan.FromMinutes(1));
        await fx.Orchestrator.ReapStuckRunningAsync(CancellationToken.None);

        (await fx.QueryAsync(db => db.DatabaseBackups
                .CountAsync(b => b.DatabaseName == "acme_db" && b.Status == BackupStatus.Running)))
            .Should().Be(0, "зависшая строка закрыта reaper'ом");

        // 3) Новый запрос той же базы + тик: должен стартовать — замок снят.
        await fx.RequestAsync("acme_db");
        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);

        (await fx.QueryAsync(db => db.DatabaseBackups
                .CountAsync(b => b.DatabaseName == "acme_db" && b.Status == BackupStatus.Running)))
            .Should().Be(1, "после reap замок-на-базу снят — новый бэкап той же базы стартует");

        await fx.DrainAsync();
    }

    [Fact]
    public async Task Late_complete_on_an_already_reaped_row_does_not_overwrite_or_double_audit()
    {
        using var fx = new Fixture();
        fx.Backup.BackupGate = new TaskCompletionSource();

        // Запрос + тик: бэкап стартует, виснет на воротах.
        var request = await fx.RequestAsync("acme_db");
        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);
        await fx.WaitUntilAsync(
            db => db.DatabaseBackups.AnyAsync(b => b.Status == BackupStatus.Running),
            "бэкап должен перейти в Running");

        // Reaper закрывает зависшую строку (TimedOut) + пишет 1 аудит.
        fx.Clock.Advance(StuckTimeout + TimeSpan.FromMinutes(1));
        await fx.Orchestrator.ReapStuckRunningAsync(CancellationToken.None);
        await fx.WaitUntilAsync(_ => Task.FromResult(fx.Audit.Entries.Count == 1), "аудит reaper'а");

        // Зависший Task.Run наконец возвращается ПОЗЖЕ → CompleteAsync должен распознать,
        // что строка уже не Running, и НЕ перетереть статус / НЕ написать второй аудит.
        fx.Backup.BackupGate.SetResult();
        await fx.WaitUntilAsync(
            async db => !await db.DatabaseBackups.AnyAsync(b => b.Status == BackupStatus.Running),
            "фоновая задача завершилась");

        var row = await fx.QueryAsync(db => db.DatabaseBackups.SingleAsync(b => b.Id == request.BackupId));
        row.Status.Should().Be(BackupStatus.Failed);
        row.FailureReason.Should().Be(BackupFailureReason.TimedOut,
            "поздний CompleteAsync не перетирает терминальный статус reaper'а");

        // Даём фоновому CompleteAsync шанс ошибочно дописать второй аудит — его быть не должно.
        await Task.Delay(50);
        fx.Audit.Entries.Should().ContainSingle("повторного аудита от позднего CompleteAsync нет");
    }

    private static DatabaseBackup Row(string database, BackupStatus status, DateTime requestedAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        InfobaseId = Guid.NewGuid(),
        DatabaseServer = "SQL01",
        DatabaseName = database,
        Status = status,
        RequestedBy = "operator",
        RequestedAtUtc = requestedAtUtc,
    };

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _sp;

        public Fixture()
        {
            var services = new ServiceCollection();
            var dbName = $"backup-reaper-{Guid.NewGuid():N}";
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddScoped<IAuditLogger>(_ => Audit);
            _sp = services.BuildServiceProvider();

            Settings = Substitute.For<ISettingsSnapshot>();
            Settings.GetString(SettingKey.BackupFolderPath).Returns(@"D:\Backups");
            Clock = new MutableClock(new DateTimeOffset(Start, TimeSpan.Zero));

            Orchestrator = new BackupOrchestrator(
                _sp.GetRequiredService<IServiceScopeFactory>(),
                Backup,
                Settings,
                Clock,
                NullLogger<BackupOrchestrator>.Instance);
        }

        public BackupOrchestrator Orchestrator { get; }
        public FakeSqlBackupService Backup { get; } = new();
        public TestHelpers.CapturingAuditLogger Audit { get; } = new();
        public ISettingsSnapshot Settings { get; }
        public MutableClock Clock { get; }

        public Task<BackupRequestResult> RequestAsync(string database) =>
            Orchestrator.RequestAsync(Guid.NewGuid(), "SQL01", database, "operator", CancellationToken.None);

        public async Task SeedAsync(params DatabaseBackup[] rows)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.DatabaseBackups.AddRange(rows);
            await db.SaveChangesAsync();
        }

        public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = _sp.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public async Task WaitUntilAsync(Func<AppDbContext, Task<bool>> condition, string because)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (await QueryAsync(condition))
                {
                    return;
                }

                await Task.Delay(10);
            }

            throw new XunitException($"Условие не наступило за 30 секунд: {because}");
        }

        public async Task DrainAsync()
        {
            Backup.BackupGate?.TrySetResult();
            await WaitUntilAsync(
                async db => !await db.DatabaseBackups.AnyAsync(b => b.Status == BackupStatus.Running),
                "все выполняющиеся бэкапы должны завершиться перед Dispose");
        }

        public void Dispose()
        {
            Orchestrator.Dispose();
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
