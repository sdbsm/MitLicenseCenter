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

// MLC-077 (ADR-27): оркестратор очереди бэкапов. Очередь = таблица DatabaseBackups;
// PumpOnceAsync — детерминированный тест-шов (образец PerfRecordingServiceTests). Сами
// бэкапы идут параллельными задачами, поэтому «выполняющийся» бэкап подвешивается на
// FakeSqlBackupService.BackupGate, а терминальные состояния дожидаются поллингом
// (WaitUntilAsync) — завершение пишется фоновой задачей, не тиком насоса.
public sealed class BackupOrchestratorTests
{
    private static readonly DateTime Start = new(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RequestAsync_inserts_queued_row()
    {
        using var fx = new Fixture();
        var infobaseId = Guid.NewGuid();

        var result = await fx.Orchestrator.RequestAsync(
            infobaseId, "SQL01", "acme_db", "operator", CancellationToken.None);

        result.Outcome.Should().Be(BackupRequestOutcome.Queued);
        var row = await fx.QueryAsync(db => db.DatabaseBackups.SingleAsync());
        row.Id.Should().Be(result.BackupId);
        row.InfobaseId.Should().Be(infobaseId);
        row.DatabaseServer.Should().Be("SQL01");
        row.DatabaseName.Should().Be("acme_db");
        row.Status.Should().Be(BackupStatus.Queued);
        row.RequestedBy.Should().Be("operator");
        row.RequestedAtUtc.Should().Be(Start);
        row.StartedAtUtc.Should().BeNull("бэкап ещё не стартовал — это сделает насос");
    }

    [Fact]
    public async Task RequestAsync_for_active_pair_returns_AlreadyActive_with_existing_id()
    {
        using var fx = new Fixture();
        var first = await fx.RequestAsync("acme_db");

        var duplicate = await fx.RequestAsync("acme_db");
        var other = await fx.RequestAsync("globex_db");

        duplicate.Outcome.Should().Be(BackupRequestOutcome.AlreadyActive);
        duplicate.BackupId.Should().Be(first.BackupId, "409-путь отдаёт id СУЩЕСТВУЮЩЕЙ активной строки");
        other.Outcome.Should().Be(BackupRequestOutcome.Queued, "замок per-db, другая база не блокируется");
        (await fx.QueryAsync(db => db.DatabaseBackups.CountAsync())).Should().Be(2);
    }

    [Fact]
    public async Task PumpOnce_starts_only_one_backup_per_database_pair()
    {
        using var fx = new Fixture();
        // Две Queued одной пары server+db могут оказаться в таблице только в обход
        // RequestAsync (он бы ответил AlreadyActive) — это defence-in-depth замка-на-базу.
        await fx.SeedAsync(
            Row("acme_db", BackupStatus.Queued, Start),
            Row("acme_db", BackupStatus.Queued, Start.AddMinutes(1)));
        fx.Backup.BackupGate = new TaskCompletionSource();

        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);

        (await fx.QueryAsync(db => db.DatabaseBackups.CountAsync(b => b.Status == BackupStatus.Running)))
            .Should().Be(1, "вторая Queued той же базы держится замком до завершения первой");
        (await fx.QueryAsync(db => db.DatabaseBackups.CountAsync(b => b.Status == BackupStatus.Queued)))
            .Should().Be(1);

        await fx.DrainAsync();
    }

    [Fact]
    public async Task PumpOnce_respects_max_parallel_and_starts_next_after_completion()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.BackupMaxParallel).Returns(2);
        fx.Backup.BackupGate = new TaskCompletionSource();
        await fx.RequestAsync("alpha_db");
        await fx.RequestAsync("beta_db");
        await fx.RequestAsync("gamma_db");

        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);

        (await fx.QueryAsync(db => db.DatabaseBackups.CountAsync(b => b.Status == BackupStatus.Running)))
            .Should().Be(2, "потолок MaxParallel=2");
        (await fx.QueryAsync(db => db.DatabaseBackups
                .Where(b => b.Status == BackupStatus.Queued)
                .Select(b => b.DatabaseName)
                .SingleAsync()))
            .Should().Be("gamma_db");

        // «Сигнал ворот»: выполняющиеся завершились → на следующем тике стартует третья.
        fx.Backup.BackupGate.SetResult();
        await fx.WaitUntilAsync(
            async db => await db.DatabaseBackups.CountAsync(b => b.Status == BackupStatus.Succeeded) == 2,
            "первые два бэкапа должны завершиться после открытия ворот");

        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);
        // Ждём именно ТЕРМИНАЛЬНЫЙ статус: Running ставится синхронно ещё в тике, а вызов
        // адаптера делает фоновая задача — ассерт по BackupCalls на промежуточном статусе гоняется.
        await fx.WaitUntilAsync(
            db => db.DatabaseBackups.AnyAsync(b => b.DatabaseName == "gamma_db" && b.Status == BackupStatus.Succeeded),
            "третья база должна стартовать следующим тиком и завершиться (ворота уже открыты)");

        fx.Backup.BackupCalls.Should().HaveCount(3);
        await fx.DrainAsync();
    }

    [Fact]
    public async Task PumpOnce_starts_oldest_queued_first()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.BackupMaxParallel).Returns(1);
        fx.Backup.BackupGate = new TaskCompletionSource();
        await fx.RequestAsync("alpha_db");
        fx.Clock.Advance(TimeSpan.FromMinutes(1));
        await fx.RequestAsync("beta_db");
        fx.Clock.Advance(TimeSpan.FromMinutes(1));
        await fx.RequestAsync("gamma_db");

        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);

        var running = await fx.QueryAsync(db => db.DatabaseBackups
            .Where(b => b.Status == BackupStatus.Running)
            .Select(b => b.DatabaseName)
            .SingleAsync());
        running.Should().Be("alpha_db", "очередь FIFO по RequestedAtUtc");

        await fx.DrainAsync();
    }

    [Fact]
    public async Task PumpOnce_rereads_max_parallel_each_tick()
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.BackupMaxParallel).Returns(1);
        fx.Backup.BackupGate = new TaskCompletionSource();
        await fx.RequestAsync("alpha_db");
        await fx.RequestAsync("beta_db");

        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);
        (await fx.QueryAsync(db => db.DatabaseBackups.CountAsync(b => b.Status == BackupStatus.Running)))
            .Should().Be(1);

        // Оператор поднял потолок — действует со следующего тика, без рестарта.
        fx.Settings.GetInt(SettingKey.BackupMaxParallel).Returns(2);
        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);
        (await fx.QueryAsync(db => db.DatabaseBackups.CountAsync(b => b.Status == BackupStatus.Running)))
            .Should().Be(2);

        await fx.DrainAsync();
    }

    [Theory]
    [InlineData(0, 2, 1)] // ниже whitelist-минимума 1 → кламп к 1
    [InlineData(99, 9, 8)] // выше whitelist-максимума 8 → кламп к 8
    public async Task PumpOnce_clamps_max_parallel_to_definition_range(
        int configured, int databases, int expectedRunning)
    {
        using var fx = new Fixture();
        fx.Settings.GetInt(SettingKey.BackupMaxParallel).Returns(configured);
        fx.Backup.BackupGate = new TaskCompletionSource();
        for (var i = 0; i < databases; i++)
        {
            await fx.RequestAsync($"db_{i}");
        }

        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);

        (await fx.QueryAsync(db => db.DatabaseBackups.CountAsync(b => b.Status == BackupStatus.Running)))
            .Should().Be(expectedRunning);

        await fx.DrainAsync();
    }

    [Fact]
    public async Task Successful_backup_finalizes_row_and_audits()
    {
        using var fx = new Fixture();
        var request = await fx.RequestAsync("acme_db");

        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);
        await fx.WaitUntilAsync(
            db => db.DatabaseBackups.AnyAsync(b => b.Status == BackupStatus.Succeeded),
            "бэкап без ворот должен завершиться сам");

        var row = await fx.QueryAsync(db => db.DatabaseBackups.SingleAsync(b => b.Id == request.BackupId));
        row.StartedAtUtc.Should().Be(Start);
        row.CompletedAtUtc.Should().Be(Start);
        row.FilePath.Should().Be(fx.Backup.NextBackupResult.FilePath);
        row.FileSizeBytes.Should().Be(fx.Backup.NextBackupResult.FileSizeBytes);
        row.FailureReason.Should().Be(BackupFailureReason.None);

        var call = fx.Backup.BackupCalls.Should().ContainSingle().Subject;
        call.Server.Should().Be("SQL01");
        call.DatabaseName.Should().Be("acme_db");
        call.FolderRoot.Should().Be(@"D:\Backups");
        call.SafetyMarginMb.Should().Be(2048, "дефолт Backup.DiskSafetyMarginMb");
        // ⚠️ keep-latest делает сам адаптер ВНУТРИ BackupAsync (после VERIFYONLY) —
        // оркестратор файлы не трогает.
        fx.Backup.DeleteCalls.Should().BeEmpty();

        await fx.WaitUntilAsync(_ => Task.FromResult(fx.Audit.Entries.Count == 1), "аудит успеха");
        var entry = fx.Audit.Entries.Single();
        entry.Action.Should().Be(AuditActionType.BackupSucceeded);
        entry.Initiator.Should().Be("operator", "initiator итога = RequestedBy строки");
        entry.Description.Should().Contain("acme_db");
    }

    [Theory]
    [InlineData(BackupFailureReason.InsufficientSpace)]
    [InlineData(BackupFailureReason.EstimateUnavailable)]
    [InlineData(BackupFailureReason.PermissionDenied)]
    [InlineData(BackupFailureReason.BackupFailed)]
    public async Task Failed_backup_records_reason_and_audits(BackupFailureReason reason)
    {
        using var fx = new Fixture();
        fx.Backup.NextBackupResult = new SqlBackupResult(
            Succeeded: false, Reason: reason, FilePath: null, FileSizeBytes: null,
            ErrorMessage: "Сообщение адаптера об ошибке.");
        var request = await fx.RequestAsync("acme_db");

        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);
        await fx.WaitUntilAsync(
            db => db.DatabaseBackups.AnyAsync(b => b.Status == BackupStatus.Failed),
            "провальный бэкап должен закрыться как Failed");

        var row = await fx.QueryAsync(db => db.DatabaseBackups.SingleAsync(b => b.Id == request.BackupId));
        row.FailureReason.Should().Be(reason);
        row.ErrorMessage.Should().Be("Сообщение адаптера об ошибке.");
        row.CompletedAtUtc.Should().Be(Start);
        row.FilePath.Should().BeNull();

        await fx.WaitUntilAsync(_ => Task.FromResult(fx.Audit.Entries.Count == 1), "аудит провала");
        fx.Audit.Entries.Single().Action.Should().Be(AuditActionType.BackupFailed);
    }

    [Fact]
    public async Task Missing_folder_path_fails_backup_without_calling_adapter()
    {
        using var fx = new Fixture();
        fx.Settings.GetString(SettingKey.BackupFolderPath).Returns((string?)null);
        var request = await fx.RequestAsync("acme_db");

        await fx.Orchestrator.PumpOnceAsync(CancellationToken.None);
        await fx.WaitUntilAsync(
            db => db.DatabaseBackups.AnyAsync(b => b.Status == BackupStatus.Failed),
            "без папки бэкап честно закрывается как Failed");

        var row = await fx.QueryAsync(db => db.DatabaseBackups.SingleAsync(b => b.Id == request.BackupId));
        row.FailureReason.Should().Be(BackupFailureReason.BackupFailed);
        row.ErrorMessage.Should().Contain("Backup.FolderPath");
        fx.Backup.BackupCalls.Should().BeEmpty("адаптер не вызывается без папки");

        await fx.WaitUntilAsync(_ => Task.FromResult(fx.Audit.Entries.Count == 1), "аудит провала");
        fx.Audit.Entries.Single().Action.Should().Be(AuditActionType.BackupFailed);
    }

    [Fact]
    public async Task RecoverInterrupted_marks_running_failed_and_keeps_queued()
    {
        using var fx = new Fixture();
        var running = Row("acme_db", BackupStatus.Running, Start.AddMinutes(-10));
        running.StartedAtUtc = Start.AddMinutes(-9);
        var queued = Row("globex_db", BackupStatus.Queued, Start.AddMinutes(-5));
        await fx.SeedAsync(running, queued);

        await fx.Orchestrator.RecoverInterruptedAsync(CancellationToken.None);

        var recovered = await fx.QueryAsync(db => db.DatabaseBackups.SingleAsync(b => b.Id == running.Id));
        recovered.Status.Should().Be(BackupStatus.Failed);
        recovered.FailureReason.Should().Be(BackupFailureReason.Interrupted);
        recovered.CompletedAtUtc.Should().Be(Start);
        recovered.ErrorMessage.Should().Contain("может быть неполным");

        var untouched = await fx.QueryAsync(db => db.DatabaseBackups.SingleAsync(b => b.Id == queued.Id));
        untouched.Status.Should().Be(BackupStatus.Queued, "Queued переподхватится насосом, recovery её не трогает");
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
            // Имя БД — снаружи лямбды: опции строятся per-scope, иначе каждый scope
            // получил бы СВОЮ пустую in-memory БД.
            var dbName = $"backup-orch-{Guid.NewGuid():N}";
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
            // Singleton-оркестратор берёт scoped IAuditLogger из scope — подменяем capturing'ом.
            services.AddScoped<IAuditLogger>(_ => Audit);
            _sp = services.BuildServiceProvider();

            Settings = Substitute.For<ISettingsSnapshot>(); // GetInt → null → дефолты + кламп
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

        // Терминальные состояния пишет фоновая задача бэкапа (вне тика насоса) — дожидаемся
        // поллингом с потолком, чтобы зависший тест падал с внятным сообщением. Потолок 30с —
        // запас под медленный CI-раннер (MLC-080: 5с флейкнул на windows-latest, PR #61);
        // зелёный путь не замедляется — ожидание выходит по условию.
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

        // Открыть ворота и дождаться, пока не останется Running — иначе Dispose уничтожит
        // семафоры под ногами фоновых задач завершения.
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
