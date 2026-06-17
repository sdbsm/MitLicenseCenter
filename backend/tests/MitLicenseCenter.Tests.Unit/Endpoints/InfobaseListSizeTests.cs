using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-185d: GET /infobases доносит текущий размер базы (currentDataBytes/currentLogBytes)
// из ПОСЛЕДНЕГО снимка телеметрии по DatabaseName (max SnapshotAtUtc). Проекция тянет
// размер коррелированным подзапросом — обязательно проверять на РЕАЛЬНОМ провайдере
// (SQLite), InMemory маскирует трансляцию подзапроса/OrderBy+First. База без снимка → null.
public sealed class InfobaseListSizeTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static IClusterClient ClusterWith()
    {
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListInfobasesAsync(Arg.Any<CancellationToken>())
            .Returns(new ClusterInfobaseDiscoveryResult(
                Array.Empty<ClusterInfobase>(), Available: true, Error: null));
        return cluster;
    }

    private static Task<Results<Ok<InfobaseListResponse>, ValidationProblem>> ListAsync(AppDbContext db) =>
        InfobasesEndpoints.ListAsync(
            db, ClusterWith(), new UnassignedInfobasesCache(),
            NullLoggerFactory.Instance, TestHelpers.FixedClock(Now),
            tenantId: null, publishStatus: null, notInCluster: null, search: null,
            page: 1, pageSize: 50, CancellationToken.None);

    [Fact]
    public async Task List_returns_size_from_latest_snapshot_per_database()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var tenant = NewTenant("Acme");
        var ib = AddBase(out var publication, tenant.Id, "Бухгалтерия", dbName: "acme_bp");

        using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(tenant);
            seed.Infobases.Add(ib);
            seed.Publications.Add(publication);
            // Два снимка ОДНОЙ базы — в ответ должен попасть поздний.
            seed.DatabaseSizeSnapshots.Add(new DatabaseSizeSnapshot
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                DatabaseName = "acme_bp",
                SnapshotAtUtc = Now.AddDays(-1),
                DataBytes = 100,
                LogBytes = 10,
            });
            seed.DatabaseSizeSnapshots.Add(new DatabaseSizeSnapshot
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                DatabaseName = "acme_bp",
                SnapshotAtUtc = Now,
                DataBytes = 2_000,
                LogBytes = 300,
            });
            seed.SaveChanges();
        }

        using var db = sqlite.NewContext();
        var result = await ListAsync(db);

        var item = result.Result.Should().BeOfType<Ok<InfobaseListResponse>>()
            .Subject.Value!.Items.Should().ContainSingle().Subject;
        item.CurrentDataBytes.Should().Be(2_000, "берётся последний снимок (max SnapshotAtUtc)");
        item.CurrentLogBytes.Should().Be(300);
    }

    [Fact]
    public async Task List_returns_null_size_when_no_snapshot()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var tenant = NewTenant("Acme");
        var ib = AddBase(out var publication, tenant.Id, "Без снимка", dbName: "no_snapshot");

        using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(tenant);
            seed.Infobases.Add(ib);
            seed.Publications.Add(publication);
            seed.SaveChanges();
        }

        using var db = sqlite.NewContext();
        var result = await ListAsync(db);

        var item = result.Result.Should().BeOfType<Ok<InfobaseListResponse>>()
            .Subject.Value!.Items.Should().ContainSingle().Subject;
        item.CurrentDataBytes.Should().BeNull("снимка нет — поля опускаются");
        item.CurrentLogBytes.Should().BeNull();
    }

    [Fact]
    public async Task List_matches_snapshot_by_database_name_not_other_database()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var tenant = NewTenant("Acme");
        var ib = AddBase(out var publication, tenant.Id, "Зарплата", dbName: "acme_zup");

        using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(tenant);
            seed.Infobases.Add(ib);
            seed.Publications.Add(publication);
            // Снимок ЧУЖОЙ базы — не должен подтянуться к acme_zup.
            seed.DatabaseSizeSnapshots.Add(new DatabaseSizeSnapshot
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                DatabaseName = "other_db",
                SnapshotAtUtc = Now,
                DataBytes = 999,
                LogBytes = 999,
            });
            seed.SaveChanges();
        }

        using var db = sqlite.NewContext();
        var result = await ListAsync(db);

        var item = result.Result.Should().BeOfType<Ok<InfobaseListResponse>>()
            .Subject.Value!.Items.Should().ContainSingle().Subject;
        item.CurrentDataBytes.Should().BeNull("снимок чужой базы не сопоставляется по DatabaseName");
        item.CurrentLogBytes.Should().BeNull();
    }

    private static Infobase AddBase(
        out Publication publication, Guid tenantId, string name, string dbName)
    {
        var ib = new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseName = dbName,
            Status = InfobaseStatus.Active,
            CreatedAt = Now,
        };
        publication = new Publication
        {
            Id = Guid.NewGuid(),
            InfobaseId = ib.Id,
            SiteName = "Default Web Site",
            VirtualPath = "/db",
            PlatformVersion = "8.3.23.1865",
            LastCheckStatus = PublicationPublishStatus.Published,
            CreatedAt = Now,
        };
        return ib;
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
