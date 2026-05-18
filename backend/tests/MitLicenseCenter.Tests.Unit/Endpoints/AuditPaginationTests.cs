using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// Server-side pagination: 60 записей, page=2 pageSize=25 → 25 строк, total=60.
public sealed class AuditPaginationTests
{
    private static readonly DateTime BaseTime = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Page_2_with_size_25_returns_correct_window()
    {
        using var db = TestHelpers.NewInMemoryDb();
        for (var i = 0; i < 60; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = BaseTime.AddMinutes(i),
                ActionType = AuditActionType.TenantUpdated,
                Initiator = "admin",
                Description = $"#{i}",
            });
        }
        await db.SaveChangesAsync();

        var result = await AuditEndpoints.ListAsync(
            db,
            actionType: null,
            tenantId: null,
            from: null,
            to: null,
            page: 2,
            pageSize: 25,
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<AuditPagedResponse>>().Subject;
        ok.Value!.Total.Should().Be(60);
        ok.Value.Page.Should().Be(2);
        ok.Value.PageSize.Should().Be(25);
        ok.Value.Items.Should().HaveCount(25);
    }

    [Fact]
    public async Task Page_3_with_size_25_returns_last_10()
    {
        using var db = TestHelpers.NewInMemoryDb();
        for (var i = 0; i < 60; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Timestamp = BaseTime.AddMinutes(i),
                ActionType = AuditActionType.TenantUpdated,
                Initiator = "admin",
                Description = $"#{i}",
            });
        }
        await db.SaveChangesAsync();

        var result = await AuditEndpoints.ListAsync(
            db,
            actionType: null,
            tenantId: null,
            from: null,
            to: null,
            page: 3,
            pageSize: 25,
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<AuditPagedResponse>>().Subject;
        ok.Value!.Items.Should().HaveCount(10, "60 - 25*2 = 10 на третьей странице");
    }

    [Theory]
    [InlineData(7, 50)]      // не из allowed → default 50
    [InlineData(0, 50)]      // 0 → default 50
    [InlineData(1000, 50)]   // больше всех allowed → default 50
    [InlineData(25, 25)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    public async Task PageSize_clamps_to_allowed_set(int requested, int expected)
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await AuditEndpoints.ListAsync(
            db,
            actionType: null,
            tenantId: null,
            from: null,
            to: null,
            page: null,
            pageSize: requested,
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<AuditPagedResponse>>().Subject;
        ok.Value!.PageSize.Should().Be(expected);
    }

    [Fact]
    public async Task Missing_page_and_pageSize_defaults_to_1_and_50()
    {
        using var db = TestHelpers.NewInMemoryDb();

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
        ok.Value!.Page.Should().Be(1);
        ok.Value.PageSize.Should().Be(50);
    }
}
