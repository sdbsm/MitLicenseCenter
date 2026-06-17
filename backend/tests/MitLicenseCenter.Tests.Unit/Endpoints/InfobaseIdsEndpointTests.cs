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

// MLC-181c: GET /infobases/ids отдаёт ВСЕ пригодные для bulk id по ТЕКУЩЕМУ фильтру без
// пагинации (вариант A «Выбрать все N по фильтру»). Доказывает единый фильтр со списком
// (те же кейсы, что у InfobaseListFilterTests), проекцию только строк с публикацией и кэп
// MaxBulkIds. Фильтрация переехала в общий BuildFilteredQueryAsync — список и /ids видят одно.
public sealed class InfobaseIdsEndpointTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

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

    private static async Task<InfobaseBulkIdsResponse> IdsAsync(
        AppDbContext db,
        Guid? tenantId = null,
        string? publishStatus = null,
        bool? notInCluster = null,
        string? search = null,
        IClusterClient? cluster = null)
    {
        var result = await InfobasesEndpoints.IdsAsync(
            db, cluster ?? ClusterWith(), new UnassignedInfobasesCache(),
            NullLoggerFactory.Instance, TestHelpers.FixedClock(Now),
            tenantId, publishStatus, notInCluster, search, CancellationToken.None);
        return result.Result.Should().BeOfType<Ok<InfobaseBulkIdsResponse>>().Subject.Value!;
    }

    [Fact]
    public async Task Ids_returns_all_eligible_rows_without_pagination()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        for (var i = 0; i < 60; i++)
        {
            AddBase(db, tenant.Id, $"Base-{i:00}", PublicationPublishStatus.Published);
        }
        await db.SaveChangesAsync();

        var response = await IdsAsync(db);

        // Список пагинирует по 50, /ids — без пагинации: все 60.
        response.Total.Should().Be(60);
        response.Items.Should().HaveCount(60);
        response.Capped.Should().BeFalse();
    }

    [Fact]
    public async Task Ids_carries_label_fields_for_bulk_dialogs()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        var ib = new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Бухгалтерия",
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseName = "acme_bp",
            Status = InfobaseStatus.Active,
            CreatedAt = Now,
        };
        var pub = new Publication
        {
            Id = Guid.NewGuid(),
            InfobaseId = ib.Id,
            SiteName = "Default Web Site",
            VirtualPath = "/acme-bp",
            PlatformVersion = "8.3.23.1865",
            LastCheckStatus = PublicationPublishStatus.Published,
            CreatedAt = Now,
        };
        db.Infobases.Add(ib);
        db.Publications.Add(pub);
        await db.SaveChangesAsync();

        var response = await IdsAsync(db);

        var item = response.Items.Should().ContainSingle().Subject;
        item.InfobaseId.Should().Be(ib.Id);
        item.PublicationId.Should().Be(pub.Id);
        item.InfobaseName.Should().Be("Бухгалтерия");
        item.SiteName.Should().Be("Default Web Site");
        item.VirtualPath.Should().Be("/acme-bp");
    }

    [Fact]
    public async Task Ids_respects_tenant_filter()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = NewTenant("Acme");
        var globex = NewTenant("Globex");
        db.Tenants.AddRange(acme, globex);
        AddBase(db, acme.Id, "Acme-1", PublicationPublishStatus.Published);
        AddBase(db, globex.Id, "Globex-1", PublicationPublishStatus.Published);
        await db.SaveChangesAsync();

        var response = await IdsAsync(db, tenantId: acme.Id);

        response.Total.Should().Be(1);
        response.Items.Should().ContainSingle().Which.InfobaseName.Should().Be("Acme-1");
    }

    [Fact]
    public async Task Ids_respects_publish_status_filter()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        AddBase(db, tenant.Id, "Pub", PublicationPublishStatus.Published);
        AddBase(db, tenant.Id, "Err", PublicationPublishStatus.Error);
        await db.SaveChangesAsync();

        var response = await IdsAsync(db, publishStatus: "Error");

        response.Total.Should().Be(1);
        response.Items.Should().ContainSingle().Which.InfobaseName.Should().Be("Err");
    }

    [Fact]
    public async Task Ids_rejects_unknown_publish_status()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await InfobasesEndpoints.IdsAsync(
            db, ClusterWith(), new UnassignedInfobasesCache(),
            NullLoggerFactory.Instance, TestHelpers.FixedClock(Now),
            null, "Garbage", null, null, CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    // ГОЧА StringComparison: на InMemory string.Contains ordinal — здесь термин подобран
    // так, что регистр совпадает; в коде ТОЛЬКО plain Contains → LIKE (регистр за CI-collation).
    [Fact]
    public async Task Ids_respects_search_filter()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        AddBase(db, tenant.Id, "Бухгалтерия", PublicationPublishStatus.Published, dbName: "acme_bp");
        AddBase(db, tenant.Id, "Зарплата", PublicationPublishStatus.Published, dbName: "acme_zup");
        await db.SaveChangesAsync();

        var byName = await IdsAsync(db, search: "Бухгал");
        byName.Total.Should().Be(1);
        byName.Items.Should().ContainSingle().Which.InfobaseName.Should().Be("Бухгалтерия");

        var byDbName = await IdsAsync(db, search: "acme_zup");
        byDbName.Total.Should().Be(1);
        byDbName.Items.Should().ContainSingle().Which.InfobaseName.Should().Be("Зарплата");
    }

    [Fact]
    public async Task Ids_search_too_long_returns_validation_problem()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await InfobasesEndpoints.IdsAsync(
            db, ClusterWith(), new UnassignedInfobasesCache(),
            NullLoggerFactory.Instance, TestHelpers.FixedClock(Now),
            null, null, null, new string('x', 201), CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    [Fact]
    public async Task Ids_respects_not_in_cluster_filter()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        var live = Guid.NewGuid();
        var ghost = Guid.NewGuid();
        AddBase(db, tenant.Id, "Живая", PublicationPublishStatus.Published, clusterId: live);
        AddBase(db, tenant.Id, "Призрак", PublicationPublishStatus.Published, clusterId: ghost);
        await db.SaveChangesAsync();

        var response = await IdsAsync(db, notInCluster: true,
            cluster: ClusterWith(new ClusterInfobase(live, "Живая", null)));

        response.Total.Should().Be(1);
        response.Items.Should().ContainSingle().Which.InfobaseName.Should().Be("Призрак");
    }

    [Fact]
    public async Task Ids_when_ras_unavailable_returns_empty_uncapped()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        AddBase(db, tenant.Id, "Призрак", PublicationPublishStatus.Published, clusterId: Guid.NewGuid());
        await db.SaveChangesAsync();

        var response = await IdsAsync(db, notInCluster: true, cluster: UnavailableCluster());

        response.Items.Should().BeEmpty();
        response.Total.Should().Be(0);
        response.Capped.Should().BeFalse();
    }

    // Только строки с публикацией: запись без публикации (рассинхрон — теоретически только
    // ручная правка БД) не пригодна для bulk и в /ids не попадает (тот же критерий, что
    // inner-Join списка).
    [Fact]
    public async Task Ids_skips_rows_without_publication()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        AddBase(db, tenant.Id, "WithPub", PublicationPublishStatus.Published);
        // База без публикации.
        db.Infobases.Add(new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "NoPub",
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseName = "db",
            Status = InfobaseStatus.Active,
            CreatedAt = Now,
        });
        await db.SaveChangesAsync();

        var response = await IdsAsync(db);

        response.Total.Should().Be(1);
        response.Items.Should().ContainSingle().Which.InfobaseName.Should().Be("WithPub");
    }

    [Fact]
    public async Task Ids_filters_compose_tenant_and_search()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = NewTenant("Acme");
        var globex = NewTenant("Globex");
        db.Tenants.AddRange(acme, globex);
        AddBase(db, acme.Id, "Shared", PublicationPublishStatus.Published);
        AddBase(db, globex.Id, "Shared", PublicationPublishStatus.Published);
        await db.SaveChangesAsync();

        var response = await IdsAsync(db, tenantId: acme.Id, search: "Shared");

        response.Total.Should().Be(1);
        response.Items.Should().ContainSingle();
    }

    // Кэп MaxBulkIds=5000: сверх него items усечён до кэпа, total — реальное число,
    // capped=true (FE по capped отказывается выбирать и просит уточнить фильтр).
    [Fact]
    public async Task Ids_caps_oversized_set_and_reports_real_total()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        const int count = 5001; // MaxBulkIds + 1
        var infobases = new List<Infobase>(count);
        var publications = new List<Publication>(count);
        for (var i = 0; i < count; i++)
        {
            var ib = new Infobase
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = $"Base-{i:0000}",
                ClusterInfobaseId = Guid.NewGuid(),
                DatabaseName = "db",
                Status = InfobaseStatus.Active,
                CreatedAt = Now,
            };
            infobases.Add(ib);
            publications.Add(new Publication
            {
                Id = Guid.NewGuid(),
                InfobaseId = ib.Id,
                SiteName = "Default Web Site",
                VirtualPath = "/db",
                PlatformVersion = "8.3.23.1865",
                LastCheckStatus = PublicationPublishStatus.Published,
                CreatedAt = Now,
            });
        }
        db.Infobases.AddRange(infobases);
        db.Publications.AddRange(publications);
        await db.SaveChangesAsync();

        var response = await IdsAsync(db);

        response.Capped.Should().BeTrue();
        response.Total.Should().Be(count); // реальное число пригодных строк
        response.Items.Should().HaveCount(5000); // усечён до кэпа
    }

    // MLC-184a — регрессия на РЕАЛЬНОЙ SQL-трансляции. InMemory-тесты выше маскируют баг:
    // ORDER BY по членам спроецированного record EF Core клиентски досортировывает. На
    // реальном провайдере (SQLite, как у DatabaseBackupPersistenceTests) такой ORDER BY не
    // транслируется и бросает InvalidOperationException. Этот тест засевает базы в SQLite,
    // зовёт IdsAsync и ожидает Ok с верным порядком по имени — на старом коде (OrderBy по
    // x.InfobaseName) падает на трансляции, на фиксе (OrderBy по x.ib.Name до проекции) зелёный.
    [Fact]
    public async Task Ids_orders_by_name_on_real_sql_translation()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var tenant = NewTenant("Acme");
        using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(tenant);
            // Порядок вставки намеренно НЕ алфавитный — проверяем именно ORDER BY.
            AddBase(seed, tenant.Id, "Гамма", PublicationPublishStatus.Published);
            AddBase(seed, tenant.Id, "Альфа", PublicationPublishStatus.Published);
            AddBase(seed, tenant.Id, "Бета", PublicationPublishStatus.Published);
            await seed.SaveChangesAsync();
        }

        using var db = sqlite.NewContext();
        var result = await InfobasesEndpoints.IdsAsync(
            db, ClusterWith(), new UnassignedInfobasesCache(),
            NullLoggerFactory.Instance, TestHelpers.FixedClock(Now),
            null, null, null, null, CancellationToken.None);

        var response = result.Result.Should().BeOfType<Ok<InfobaseBulkIdsResponse>>().Subject.Value!;
        response.Total.Should().Be(3);
        response.Items.Select(x => x.InfobaseName)
            .Should().ContainInOrder("Альфа", "Бета", "Гамма");
    }

    private static void AddBase(
        AppDbContext db, Guid tenantId, string name, PublicationPublishStatus status,
        Guid? clusterId = null, string dbName = "db")
    {
        var ib = new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            ClusterInfobaseId = clusterId ?? Guid.NewGuid(),
            DatabaseName = dbName,
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
