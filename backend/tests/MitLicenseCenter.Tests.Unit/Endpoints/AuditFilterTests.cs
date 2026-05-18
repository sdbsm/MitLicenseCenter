using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// GET /api/v1/audit — фильтры actionType и диапазона by-Timestamp.
// 10 записей разных типов; проверяем что выборка возвращает ожидаемое подмножество
// и что сортировка — по Timestamp DESC.
public sealed class AuditFilterTests
{
    private static readonly DateTime BaseTime = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Filter_by_actionType_returns_only_matching_entries()
    {
        using var db = TestHelpers.NewInMemoryDb();
        await SeedTenAsync(db);

        var result = await AuditEndpoints.ListAsync(
            db,
            actionType: nameof(AuditActionType.TenantCreated),
            tenantId: null,
            from: null,
            to: null,
            page: null,
            pageSize: null,
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<AuditPagedResponse>>().Subject;
        ok.Value!.Items.Should().OnlyContain(e => e.ActionType == AuditActionType.TenantCreated);
        ok.Value.Total.Should().Be(ok.Value.Items.Count);
    }

    [Fact]
    public async Task Filter_by_date_range_excludes_entries_outside_window()
    {
        using var db = TestHelpers.NewInMemoryDb();
        await SeedTenAsync(db);

        var from = BaseTime.AddHours(2);
        var to = BaseTime.AddHours(5);
        var result = await AuditEndpoints.ListAsync(
            db,
            actionType: null,
            tenantId: null,
            from: from,
            to: to,
            page: null,
            pageSize: null,
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<AuditPagedResponse>>().Subject;
        ok.Value!.Items.Should().OnlyContain(e => e.Timestamp >= from && e.Timestamp <= to);
        ok.Value.Items.Should().HaveCount(4, "часы 2,3,4,5 попадают в диапазон [2..5]");
    }

    [Fact]
    public async Task Combined_actionType_and_date_range_filters_intersect()
    {
        using var db = TestHelpers.NewInMemoryDb();
        await SeedTenAsync(db);

        var result = await AuditEndpoints.ListAsync(
            db,
            actionType: nameof(AuditActionType.InfobaseCreated),
            tenantId: null,
            from: BaseTime,
            to: BaseTime.AddHours(4),
            page: null,
            pageSize: null,
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<AuditPagedResponse>>().Subject;
        ok.Value!.Items.Should().OnlyContain(e =>
            e.ActionType == AuditActionType.InfobaseCreated
            && e.Timestamp >= BaseTime
            && e.Timestamp <= BaseTime.AddHours(4));
    }

    [Fact]
    public async Task Invalid_actionType_returns_ValidationProblem()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await AuditEndpoints.ListAsync(
            db,
            actionType: "NotARealAction",
            tenantId: null,
            from: null,
            to: null,
            page: null,
            pageSize: null,
            ct: CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    [Fact]
    public async Task To_before_from_returns_ValidationProblem()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await AuditEndpoints.ListAsync(
            db,
            actionType: null,
            tenantId: null,
            from: BaseTime.AddHours(5),
            to: BaseTime,
            page: null,
            pageSize: null,
            ct: CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    [Fact]
    public async Task Items_returned_in_descending_Timestamp_order()
    {
        using var db = TestHelpers.NewInMemoryDb();
        await SeedTenAsync(db);

        var result = await AuditEndpoints.ListAsync(
            db,
            actionType: null,
            tenantId: null,
            from: null,
            to: null,
            page: null,
            pageSize: null,
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<AuditPagedResponse>>().Subject;
        ok.Value!.Items.Should().BeInDescendingOrder(e => e.Timestamp);
    }

    private static async Task SeedTenAsync(MitLicenseCenter.Infrastructure.Persistence.AppDbContext db)
    {
        var actions = new[]
        {
            AuditActionType.TenantCreated,
            AuditActionType.TenantUpdated,
            AuditActionType.TenantDeleted,
            AuditActionType.InfobaseCreated,
            AuditActionType.InfobaseCreated,
            AuditActionType.InfobaseUpdated,
            AuditActionType.PublicationCreated,
            AuditActionType.PublicationUpdated,
            AuditActionType.AdminLoggedIn,
            AuditActionType.AdminLoggedOut,
        };

        for (var i = 0; i < actions.Length; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = BaseTime.AddHours(i),
                ActionType = actions[i],
                Initiator = "admin",
                Description = $"Запись #{i}",
            });
        }
        await db.SaveChangesAsync();
    }
}
