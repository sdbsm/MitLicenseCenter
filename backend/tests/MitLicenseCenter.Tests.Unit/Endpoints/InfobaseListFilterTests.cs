using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-090: GET /infobases фильтрует по статусу публикации (server-side, до пагинации).
// Значение enum'а валидируется руками (DataAnnotations в minimal API не прогоняются) —
// мусор отвечает 400, как actionType на /audit.
public sealed class InfobaseListFilterTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task List_filters_by_publish_status()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        AddBase(db, tenant.Id, "Published-1", PublicationPublishStatus.Published);
        AddBase(db, tenant.Id, "NotPublished-1", PublicationPublishStatus.NotPublished);
        AddBase(db, tenant.Id, "Error-1", PublicationPublishStatus.Error);
        await db.SaveChangesAsync();

        var result = await InfobasesEndpoints.ListAsync(
            db, tenantId: null, publishStatus: "NotPublished", page: 1, pageSize: 50, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<InfobaseListResponse>>().Subject;
        ok.Value!.Total.Should().Be(1);
        ok.Value.Items.Should().ContainSingle()
            .Which.Publication.LastCheckStatus.Should().Be(PublicationPublishStatus.NotPublished);
    }

    [Fact]
    public async Task List_without_status_returns_all()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        AddBase(db, tenant.Id, "A", PublicationPublishStatus.Published);
        AddBase(db, tenant.Id, "B", PublicationPublishStatus.Error);
        await db.SaveChangesAsync();

        var result = await InfobasesEndpoints.ListAsync(
            db, tenantId: null, publishStatus: null, page: 1, pageSize: 50, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<InfobaseListResponse>>()
            .Subject.Value!.Total.Should().Be(2);
    }

    [Fact]
    public async Task List_status_filter_composes_with_tenant_filter()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = NewTenant("Acme");
        var globex = NewTenant("Globex");
        db.Tenants.AddRange(acme, globex);
        AddBase(db, acme.Id, "Acme-Err", PublicationPublishStatus.Error);
        AddBase(db, globex.Id, "Globex-Err", PublicationPublishStatus.Error);
        await db.SaveChangesAsync();

        var result = await InfobasesEndpoints.ListAsync(
            db, tenantId: acme.Id, publishStatus: "Error", page: 1, pageSize: 50, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<InfobaseListResponse>>().Subject;
        ok.Value!.Items.Should().ContainSingle().Which.Name.Should().Be("Acme-Err");
    }

    [Theory]
    [InlineData("Garbage")]
    [InlineData("99")]
    public async Task List_rejects_unknown_publish_status(string bad)
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await InfobasesEndpoints.ListAsync(
            db, tenantId: null, publishStatus: bad, page: 1, pageSize: 50, CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    [Fact]
    public async Task List_accepts_publish_status_case_insensitively()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        AddBase(db, tenant.Id, "P", PublicationPublishStatus.Published);
        await db.SaveChangesAsync();

        var result = await InfobasesEndpoints.ListAsync(
            db, tenantId: null, publishStatus: "published", page: 1, pageSize: 50, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<InfobaseListResponse>>()
            .Subject.Value!.Total.Should().Be(1);
    }

    private static void AddBase(
        AppDbContext db, Guid tenantId, string name, PublicationPublishStatus status)
    {
        var ib = new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseName = "db",
            Status = InfobaseStatus.Active,
            CreatedAt = Now,
        };
        db.Infobases.Add(ib);
        db.Publications.Add(new Publication
        {
            Id = Guid.NewGuid(),
            InfobaseId = ib.Id,
            SiteName = "Default Web Site",
            VirtualPath = "/db",
            PlatformVersion = "8.3.23.1865",
            LastCheckStatus = status,
            CreatedAt = Now,
        });
    }

    private static Tenant NewTenant(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxConcurrentLicenses = 10,
        IsActive = true,
        CreatedAt = Now,
    };
}
