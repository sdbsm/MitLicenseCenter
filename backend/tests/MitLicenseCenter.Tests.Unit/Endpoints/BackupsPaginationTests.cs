using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Infrastructure.Backups.Testing;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-130 (BE-17): серверная пагинация списка бэкапов.
public sealed class BackupsPaginationTests
{
    private static readonly DateTime BaseTime = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Page_2_with_size_10_returns_correct_window()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var infobaseId = Guid.NewGuid();
        for (var i = 0; i < 30; i++)
        {
            db.DatabaseBackups.Add(MakeBackup(infobaseId, $"db_{i:D2}", BaseTime.AddMinutes(i)));
        }
        await db.SaveChangesAsync();

        var result = await BackupsEndpoints.ListAsync(
            infobaseId: null,
            page: 2,
            pageSize: 10,
            search: null,
            db: db,
            backupService: new FakeSqlBackupService(),
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<BackupsPagedResponse>>().Subject;
        ok.Value!.Total.Should().Be(30);
        ok.Value.Page.Should().Be(2);
        ok.Value.PageSize.Should().Be(10);
        ok.Value.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task Page_beyond_last_returns_empty_without_crash()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var infobaseId = Guid.NewGuid();
        db.DatabaseBackups.Add(MakeBackup(infobaseId, "db_0", BaseTime));
        await db.SaveChangesAsync();

        var result = await BackupsEndpoints.ListAsync(
            infobaseId: null,
            page: 99,
            pageSize: 25,
            search: null,
            db: db,
            backupService: new FakeSqlBackupService(),
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<BackupsPagedResponse>>().Subject;
        ok.Value!.Items.Should().BeEmpty("страница за пределами данных — пустая, не крэш");
        ok.Value.Total.Should().Be(1);
    }

    [Fact]
    public async Task Missing_page_and_pageSize_defaults_to_1_and_25()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await BackupsEndpoints.ListAsync(
            infobaseId: null,
            page: null,
            pageSize: null,
            search: null,
            db: db,
            backupService: new FakeSqlBackupService(),
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<BackupsPagedResponse>>().Subject;
        ok.Value!.Page.Should().Be(1);
        ok.Value.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task Total_reflects_infobase_filter()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        db.DatabaseBackups.Add(MakeBackup(a, "db_a1", BaseTime));
        db.DatabaseBackups.Add(MakeBackup(a, "db_a2", BaseTime.AddMinutes(1)));
        db.DatabaseBackups.Add(MakeBackup(b, "db_b1", BaseTime.AddMinutes(2)));
        await db.SaveChangesAsync();

        var result = await BackupsEndpoints.ListAsync(
            infobaseId: a,
            page: 1,
            pageSize: 25,
            search: null,
            db: db,
            backupService: new FakeSqlBackupService(),
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<BackupsPagedResponse>>().Subject;
        ok.Value!.Total.Should().Be(2, "только бэкапы инфобазы A");
        ok.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_filters_by_database_name_same_case()
    {
        // InMemory-тест: проверяем совпадение того же регистра.
        // Кросс-регистр гарантирует CI-collation SQL Server — InMemory его не эмулирует.
        using var db = TestHelpers.NewInMemoryDb();
        var iid = Guid.NewGuid();
        db.DatabaseBackups.Add(MakeBackup(iid, "acme_bp", BaseTime));
        db.DatabaseBackups.Add(MakeBackup(iid, "globex_crm", BaseTime.AddMinutes(1)));
        db.DatabaseBackups.Add(MakeBackup(iid, "acme_hr", BaseTime.AddMinutes(2)));
        await db.SaveChangesAsync();

        var result = await BackupsEndpoints.ListAsync(
            infobaseId: null,
            page: 1,
            pageSize: 25,
            search: "acme",
            db: db,
            backupService: new FakeSqlBackupService(),
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<BackupsPagedResponse>>().Subject;
        ok.Value!.Total.Should().Be(2);
        ok.Value.Items.Select(b => b.DatabaseName).Should().BeEquivalentTo(["acme_bp", "acme_hr"]);
    }

    [Fact]
    public async Task Search_too_long_returns_validation_problem()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await BackupsEndpoints.ListAsync(
            infobaseId: null,
            page: 1,
            pageSize: 25,
            search: new string('x', 201),
            db: db,
            backupService: new FakeSqlBackupService(),
            ct: CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>(
            "строка поиска длиннее 200 символов → ValidationProblem");
    }

    private static DatabaseBackup MakeBackup(Guid infobaseId, string dbName, DateTime requestedAt) => new()
    {
        Id = Guid.NewGuid(),
        InfobaseId = infobaseId,
        DatabaseServer = "SQL01",
        DatabaseName = dbName,
        Status = BackupStatus.Succeeded,
        RequestedBy = "operator",
        RequestedAtUtc = requestedAt,
    };
}
