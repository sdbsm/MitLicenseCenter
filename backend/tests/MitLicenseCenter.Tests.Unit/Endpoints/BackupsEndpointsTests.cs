using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Backups.Testing;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-077 (ADR-27): эндпоинты бэкапов. Стиль проекта — invoke internal static handler
// напрямую. Постановка идёт через IBackupOrchestrator (substitute — проводка исхода),
// server-side удаление файла — через FakeSqlBackupService; список/просмотр/удаление читают
// AppDbContext напрямую (vertical slice ADR-20). Поведение очереди — BackupOrchestratorTests.
public sealed class BackupsEndpointsTests
{
    private static readonly DateTime Now = new(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task List_orders_newest_first_and_filters_by_infobase()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var infobaseId = Guid.NewGuid();
        var older = Row(infobaseId, "acme_db", BackupStatus.Succeeded, Now.AddHours(-2));
        var newer = Row(infobaseId, "acme_db", BackupStatus.Queued, Now.AddHours(-1));
        var foreign = Row(Guid.NewGuid(), "globex_db", BackupStatus.Succeeded, Now);
        db.DatabaseBackups.AddRange(older, newer, foreign);
        await db.SaveChangesAsync();

        var all = await BackupsEndpoints.ListAsync(null, db, CancellationToken.None);
        var filtered = await BackupsEndpoints.ListAsync(infobaseId, db, CancellationToken.None);

        all.Value!.Select(b => b.Id).Should().Equal([foreign.Id, newer.Id, older.Id], "свежие сверху");
        filtered.Value!.Select(b => b.Id).Should().Equal([newer.Id, older.Id]);
    }

    [Fact]
    public async Task Get_returns_summary()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var row = Row(Guid.NewGuid(), "acme_db", BackupStatus.Failed, Now);
        row.FailureReason = BackupFailureReason.InsufficientSpace;
        row.ErrorMessage = "Недостаточно места.";
        db.DatabaseBackups.Add(row);
        await db.SaveChangesAsync();

        var result = await BackupsEndpoints.GetAsync(row.Id, db, CancellationToken.None);

        var summary = result.Result.Should().BeOfType<Ok<BackupSummary>>().Subject.Value!;
        summary.Id.Should().Be(row.Id);
        summary.Status.Should().Be(BackupStatus.Failed);
        summary.FailureReason.Should().Be(BackupFailureReason.InsufficientSpace);
        summary.ErrorMessage.Should().Be("Недостаточно места.");
    }

    [Fact]
    public async Task Get_missing_returns_not_found()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await BackupsEndpoints.GetAsync(Guid.NewGuid(), db, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Start_with_unknown_infobase_returns_not_found()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var orchestrator = Substitute.For<IBackupOrchestrator>();

        var result = await BackupsEndpoints.StartAsync(
            new StartBackupRequest(Guid.NewGuid()), orchestrator, ConfiguredSettings(), db,
            new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        await orchestrator.DidNotReceiveWithAnyArgs()
            .RequestAsync(default, default!, default!, default!, default);
    }

    [Fact]
    public async Task Start_without_configured_folder_returns_conflict()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var infobase = NewInfobase();
        db.Infobases.Add(infobase);
        await db.SaveChangesAsync();
        var orchestrator = Substitute.For<IBackupOrchestrator>();
        var settings = Substitute.For<ISettingsSnapshot>(); // GetString → null

        var result = await BackupsEndpoints.StartAsync(
            new StartBackupRequest(infobase.Id), orchestrator, settings, db,
            new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext(), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.BackupFolderNotConfigured);
        await orchestrator.DidNotReceiveWithAnyArgs()
            .RequestAsync(default, default!, default!, default!, default);
    }

    [Fact]
    public async Task Start_without_configured_sql_server_returns_conflict()
    {
        // MLC-088: сервер БД больше не хранится per-база — без настройки Sql.Server бэкап
        // брать не с чего; честный 409 до постановки в очередь (папка задана, сервер нет).
        using var db = TestHelpers.NewInMemoryDb();
        var infobase = NewInfobase();
        db.Infobases.Add(infobase);
        await db.SaveChangesAsync();
        var orchestrator = Substitute.For<IBackupOrchestrator>();
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.BackupFolderPath).Returns(@"D:\Backups"); // Sql.Server → null

        var result = await BackupsEndpoints.StartAsync(
            new StartBackupRequest(infobase.Id), orchestrator, settings, db,
            new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext(), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.SqlServerNotConfigured);
        await orchestrator.DidNotReceiveWithAnyArgs()
            .RequestAsync(default, default!, default!, default!, default);
    }

    [Fact]
    public async Task Start_when_database_already_active_returns_conflict_without_audit()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var infobase = NewInfobase();
        db.Infobases.Add(infobase);
        await db.SaveChangesAsync();
        var orchestrator = Substitute.For<IBackupOrchestrator>();
        orchestrator.RequestAsync(infobase.Id, "SQL01", infobase.DatabaseName,
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BackupRequestResult(BackupRequestOutcome.AlreadyActive, Guid.NewGuid()));
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await BackupsEndpoints.StartAsync(
            new StartBackupRequest(infobase.Id), orchestrator, ConfiguredSettings(), db,
            audit, TestHelpers.NewHttpContext(), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.BackupActive);
        audit.Entries.Should().BeEmpty("дубль не ставится в очередь — фиксировать нечего");
    }

    [Fact]
    public async Task Start_returns_created_and_audits_with_tenant()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var infobase = NewInfobase();
        db.Infobases.Add(infobase);
        var backupId = Guid.NewGuid();
        // Оркестратор персистит строку в своём scope; substitute не персистит — эмулируем.
        db.DatabaseBackups.Add(Row(infobase.Id, infobase.DatabaseName, BackupStatus.Queued, Now, backupId));
        await db.SaveChangesAsync();
        var orchestrator = Substitute.For<IBackupOrchestrator>();
        orchestrator.RequestAsync(infobase.Id, "SQL01", infobase.DatabaseName,
                "operator", Arg.Any<CancellationToken>())
            .Returns(new BackupRequestResult(BackupRequestOutcome.Queued, backupId));
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await BackupsEndpoints.StartAsync(
            new StartBackupRequest(infobase.Id), orchestrator, ConfiguredSettings(), db,
            audit, TestHelpers.NewHttpContext("operator"), CancellationToken.None);

        var created = result.Result.Should().BeOfType<Created<BackupSummary>>().Subject;
        created.Value!.Id.Should().Be(backupId);
        created.Value!.Status.Should().Be(BackupStatus.Queued);
        created.Location.Should().Be($"/api/v1/backups/{backupId}");

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActionType.BackupRequested);
        entry.Initiator.Should().Be("operator");
        entry.TenantId.Should().Be(infobase.TenantId, "запрос — единственная аудит-запись со связкой с клиентом");
        entry.Description.Should().Contain(infobase.DatabaseName);
    }

    [Fact]
    public async Task Delete_missing_returns_not_found()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await BackupsEndpoints.DeleteAsync(
            Guid.NewGuid(), db, new FakeSqlBackupService(), new TestHelpers.CapturingAuditLogger(),
            TestHelpers.FixedClock(Now), TestHelpers.NewHttpContext(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Delete_running_returns_conflict_and_keeps_row()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var row = Row(Guid.NewGuid(), "acme_db", BackupStatus.Running, Now);
        db.DatabaseBackups.Add(row);
        await db.SaveChangesAsync();

        var result = await BackupsEndpoints.DeleteAsync(
            row.Id, db, new FakeSqlBackupService(), new TestHelpers.CapturingAuditLogger(),
            TestHelpers.FixedClock(Now), TestHelpers.NewHttpContext(), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.BackupActive);
        db.DatabaseBackups.Count().Should().Be(1, "идущий бэкап удалять нельзя");
    }

    [Fact]
    public async Task Delete_removes_file_and_row_and_audits()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var row = Row(Guid.NewGuid(), "acme_db", BackupStatus.Succeeded, Now.AddHours(-1));
        row.FilePath = @"D:\Backups\acme_db\acme_db_20260609_110000.bak";
        db.DatabaseBackups.Add(row);
        await db.SaveChangesAsync();
        var fake = new FakeSqlBackupService();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await BackupsEndpoints.DeleteAsync(
            row.Id, db, fake, audit, TestHelpers.FixedClock(Now),
            TestHelpers.NewHttpContext(), CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();
        // keep-latest-1: в подпапке базы лежит ровно этот файл → cutoff=сейчас сносит его.
        var call = fake.DeleteCalls.Should().ContainSingle().Subject;
        call.Server.Should().Be("SQL01");
        call.FolderPath.Should().Be(@"D:\Backups\acme_db");
        call.CutoffUtc.Should().Be(Now);
        db.DatabaseBackups.Count().Should().Be(0);

        var entry = audit.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(AuditActionType.BackupDeleted);
        entry.Initiator.Should().Be("admin");
    }

    [Fact]
    public async Task Delete_keeps_row_when_file_removal_fails()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var row = Row(Guid.NewGuid(), "acme_db", BackupStatus.Succeeded, Now.AddHours(-1));
        row.FilePath = @"D:\Backups\acme_db\acme_db_20260609_110000.bak";
        db.DatabaseBackups.Add(row);
        await db.SaveChangesAsync();
        var fake = new FakeSqlBackupService
        {
            NextDeleteResult = new SqlDeleteResult(Succeeded: false, ErrorMessage: "xp_delete_file failed"),
        };
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await BackupsEndpoints.DeleteAsync(
            row.Id, db, fake, audit, TestHelpers.FixedClock(Now),
            TestHelpers.NewHttpContext(), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.BackupDeleteFailed);
        db.DatabaseBackups.Count().Should().Be(1, "файл не удалён → запись остаётся, чтобы .bak не осиротел");
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_without_file_skips_server_side_cleanup()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var row = Row(Guid.NewGuid(), "acme_db", BackupStatus.Failed, Now.AddHours(-1));
        row.FailureReason = BackupFailureReason.InsufficientSpace;
        db.DatabaseBackups.Add(row);
        await db.SaveChangesAsync();
        var fake = new FakeSqlBackupService();

        var result = await BackupsEndpoints.DeleteAsync(
            row.Id, db, fake, new TestHelpers.CapturingAuditLogger(), TestHelpers.FixedClock(Now),
            TestHelpers.NewHttpContext(), CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();
        fake.DeleteCalls.Should().BeEmpty("у провального бэкапа нет файла — чистить нечего");
        db.DatabaseBackups.Count().Should().Be(0);
    }

    private static ISettingsSnapshot ConfiguredSettings()
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.BackupFolderPath).Returns(@"D:\Backups");
        // MLC-088: сервер БД — единый SQL-инстанс из настройки (не per-база).
        settings.GetString(SettingKey.SqlServer).Returns("SQL01");
        return settings;
    }

    private static Infobase NewInfobase() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Name = "Acme",
        ClusterInfobaseId = Guid.NewGuid(),
        DatabaseName = "acme_db",
        CreatedAt = Now,
    };

    private static DatabaseBackup Row(
        Guid infobaseId, string database, BackupStatus status, DateTime requestedAtUtc, Guid? id = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            InfobaseId = infobaseId,
            DatabaseServer = "SQL01",
            DatabaseName = database,
            Status = status,
            RequestedBy = "operator",
            RequestedAtUtc = requestedAtUtc,
        };
}
