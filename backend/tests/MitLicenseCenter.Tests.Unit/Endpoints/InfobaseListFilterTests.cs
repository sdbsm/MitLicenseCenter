using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-090: GET /infobases фильтрует по статусу публикации (server-side, до пагинации).
// Значение enum'а валидируется руками (DataAnnotations в minimal API не прогоняются) —
// мусор отвечает 400, как actionType на /audit.
// MLC-150: тот же эндпоинт несёт серверный фильтр notInCluster=true (обратный дрейф) —
// см. отдельные тесты ниже.
public sealed class InfobaseListFilterTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    // Кластер-заглушка: Available с переданными базами (по умолчанию пуст — для тестов,
    // не использующих notInCluster, снапшот нерелевантен, RAS не опрашивается).
    private static IClusterClient ClusterWith(params ClusterInfobase[] infobases)
    {
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListInfobasesAsync(Arg.Any<CancellationToken>())
            .Returns(new ClusterInfobaseDiscoveryResult(infobases, Available: true, Error: null));
        return cluster;
    }

    private static IClusterClient UnavailableCluster()
    {
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListInfobasesAsync(Arg.Any<CancellationToken>())
            .Returns(new ClusterInfobaseDiscoveryResult(
                Array.Empty<ClusterInfobase>(), Available: false, Error: "stderr"));
        return cluster;
    }

    private static Task<Results<Ok<InfobaseListResponse>, ValidationProblem>> ListAsync(
        AppDbContext db,
        Guid? tenantId = null,
        string? publishStatus = null,
        bool? notInCluster = null,
        IClusterClient? cluster = null,
        int page = 1,
        int pageSize = 50) =>
        InfobasesEndpoints.ListAsync(
            db, cluster ?? ClusterWith(), new UnassignedInfobasesCache(),
            NullLoggerFactory.Instance, TestHelpers.FixedClock(Now),
            tenantId, publishStatus, notInCluster, page, pageSize, CancellationToken.None);

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

        var result = await ListAsync(db, publishStatus: "NotPublished");

        var ok = result.Result.Should().BeOfType<Ok<InfobaseListResponse>>().Subject;
        ok.Value!.Total.Should().Be(1);
        ok.Value.Items.Should().ContainSingle()
            .Which.Publication.LastCheckStatus.Should().Be(PublicationPublishStatus.NotPublished);
        ok.Value.ClusterAvailable.Should().BeNull("кластер нерелевантен без notInCluster");
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

        var result = await ListAsync(db);

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

        var result = await ListAsync(db, tenantId: acme.Id, publishStatus: "Error");

        var ok = result.Result.Should().BeOfType<Ok<InfobaseListResponse>>().Subject;
        ok.Value!.Items.Should().ContainSingle().Which.Name.Should().Be("Acme-Err");
    }

    [Theory]
    [InlineData("Garbage")]
    [InlineData("99")]
    public async Task List_rejects_unknown_publish_status(string bad)
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await ListAsync(db, publishStatus: bad);

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

        var result = await ListAsync(db, publishStatus: "published");

        result.Result.Should().BeOfType<Ok<InfobaseListResponse>>()
            .Subject.Value!.Total.Should().Be(1);
    }

    // ── MLC-150: серверный фильтр «не найдена в кластере» (обратный дрейф) ────────────

    [Fact]
    public async Task NotInCluster_filter_returns_only_records_absent_from_cluster()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        var live = Guid.NewGuid();
        var ghost = Guid.NewGuid();
        AddBase(db, tenant.Id, "Живая", PublicationPublishStatus.Published, clusterId: live);
        AddBase(db, tenant.Id, "Призрак", PublicationPublishStatus.Published, clusterId: ghost);
        await db.SaveChangesAsync();

        // Кластер содержит только «Живую» — «Призрак» отсутствует ⇒ попадает в фильтр.
        var result = await ListAsync(db, notInCluster: true,
            cluster: ClusterWith(new ClusterInfobase(live, "Живая", null)));

        var ok = result.Result.Should().BeOfType<Ok<InfobaseListResponse>>().Subject;
        ok.Value!.ClusterAvailable.Should().BeTrue();
        ok.Value.Total.Should().Be(1);
        ok.Value.Items.Should().ContainSingle().Which.Name.Should().Be("Призрак");
    }

    [Fact]
    public async Task NotInCluster_filter_composes_with_tenant_filter()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = NewTenant("Acme");
        var globex = NewTenant("Globex");
        db.Tenants.AddRange(acme, globex);
        var acmeGhost = Guid.NewGuid();
        var globexGhost = Guid.NewGuid();
        AddBase(db, acme.Id, "Acme-Ghost", PublicationPublishStatus.Published, clusterId: acmeGhost);
        AddBase(db, globex.Id, "Globex-Ghost", PublicationPublishStatus.Published, clusterId: globexGhost);
        await db.SaveChangesAsync();

        // Кластер пуст — обе записи «пропавшие», но фильтр по клиенту оставляет только Acme.
        var result = await ListAsync(db, tenantId: acme.Id, notInCluster: true, cluster: ClusterWith());

        var ok = result.Result.Should().BeOfType<Ok<InfobaseListResponse>>().Subject;
        ok.Value!.Items.Should().ContainSingle().Which.Name.Should().Be("Acme-Ghost");
    }

    [Fact]
    public async Task NotInCluster_when_ras_unavailable_returns_empty_with_cluster_available_false()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        // Запись с UUID вне кластера, но RAS недоступен — НЕ ложный пустой «0 найдено».
        AddBase(db, tenant.Id, "Призрак", PublicationPublishStatus.Published, clusterId: Guid.NewGuid());
        await db.SaveChangesAsync();

        var result = await ListAsync(db, notInCluster: true, cluster: UnavailableCluster());

        var ok = result.Result.Should().BeOfType<Ok<InfobaseListResponse>>().Subject;
        ok.Value!.ClusterAvailable.Should().BeFalse("RAS недоступен — не отличить «нет пропавших» от «не знаем»");
        ok.Value.Items.Should().BeEmpty();
        ok.Value.Total.Should().Be(0);
    }

    private static void AddBase(
        AppDbContext db, Guid tenantId, string name, PublicationPublishStatus status,
        Guid? clusterId = null)
    {
        var ib = new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            ClusterInfobaseId = clusterId ?? Guid.NewGuid(),
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
