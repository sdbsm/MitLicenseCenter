using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// Имя инфобазы уникально в пределах клиента, но не глобально: «Бухгалтерия»
// у Acme и у Beta — два разных tenant'а — должны сосуществовать.
public sealed class InfobaseUniquePerTenantTests
{
    private static CreateInfobaseRequest BuildRequest(Guid tenantId, string name, Guid? clusterInfobaseId = null) =>
        new(
            TenantId: tenantId,
            Name: name,
            ClusterInfobaseId: clusterInfobaseId ?? Guid.NewGuid(),
            DatabaseName: "ib",
            Status: InfobaseStatus.Active,
            Publication: new CreatePublicationRequest(
                SiteName: "Default Web Site",
                VirtualPath: "/ib",
                PlatformVersion: "8.3.23.1865",
                PhysicalPathOverride: null));

    private static Tenant SeedTenant(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxConcurrentLicenses = 10,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task Same_name_in_two_tenants_is_allowed()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = SeedTenant("Acme");
        var beta = SeedTenant("Beta");
        db.Tenants.AddRange(acme, beta);
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        var clock = TestHelpers.FixedClock(new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc));

        var r1 = await InfobasesEndpoints.CreateAsync(
            BuildRequest(acme.Id, "Бухгалтерия"),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            clock,
            CancellationToken.None);
        var r2 = await InfobasesEndpoints.CreateAsync(
            BuildRequest(beta.Id, "Бухгалтерия"),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            clock,
            CancellationToken.None);

        r1.Result.Should().BeOfType<Created<InfobaseDetailResponse>>();
        r2.Result.Should().BeOfType<Created<InfobaseDetailResponse>>();
    }

    [Fact]
    public async Task Same_name_in_one_tenant_returns_409_NAME_DUPLICATE_IN_TENANT()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = SeedTenant("Acme");
        db.Tenants.Add(acme);
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        var clock = TestHelpers.FixedClock(new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc));

        var first = await InfobasesEndpoints.CreateAsync(
            BuildRequest(acme.Id, "Бухгалтерия"),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            clock,
            CancellationToken.None);
        first.Result.Should().BeOfType<Created<InfobaseDetailResponse>>();

        var second = await InfobasesEndpoints.CreateAsync(
            BuildRequest(acme.Id, "Бухгалтерия"),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            clock,
            CancellationToken.None);

        var conflict = second.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.NameDuplicateInTenant);
    }

    [Fact]
    public async Task Create_with_unknown_tenant_returns_NotFound()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var audit = new TestHelpers.CapturingAuditLogger();
        var clock = TestHelpers.FixedClock(new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc));

        var result = await InfobasesEndpoints.CreateAsync(
            BuildRequest(Guid.NewGuid(), "Бухгалтерия"),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            clock,
            CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task Same_cluster_infobase_in_two_tenants_returns_409_INFOBASE_ALREADY_ASSIGNED()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = SeedTenant("Acme");
        var beta = SeedTenant("Beta");
        db.Tenants.AddRange(acme, beta);
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        var clock = TestHelpers.FixedClock(new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc));
        var clusterId = Guid.NewGuid();

        var first = await InfobasesEndpoints.CreateAsync(
            BuildRequest(acme.Id, "Бухгалтерия Acme", clusterId),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            clock,
            CancellationToken.None);
        first.Result.Should().BeOfType<Created<InfobaseDetailResponse>>();

        // Та же база кластера, другой клиент, другое имя — всё равно конфликт.
        var second = await InfobasesEndpoints.CreateAsync(
            BuildRequest(beta.Id, "Бухгалтерия Beta", clusterId),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            clock,
            CancellationToken.None);

        var conflict = second.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.InfobaseAlreadyAssigned);
    }

    [Fact]
    public async Task Update_keeping_own_cluster_infobase_does_not_conflict()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = SeedTenant("Acme");
        db.Tenants.Add(acme);
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        var clock = TestHelpers.FixedClock(new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc));
        var clusterId = Guid.NewGuid();

        var created = await InfobasesEndpoints.CreateAsync(
            BuildRequest(acme.Id, "Бухгалтерия", clusterId),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            clock,
            CancellationToken.None);
        var detail = created.Result.Should().BeOfType<Created<InfobaseDetailResponse>>().Subject;
        var id = detail.Value!.Infobase.Id;

        var update = await InfobasesEndpoints.UpdateAsync(
            id,
            new UpdateInfobaseRequest(
                Name: "Бухгалтерия 2.0",
                ClusterInfobaseId: clusterId,
                DatabaseName: "ib",
                Status: InfobaseStatus.Active,
                Publication: new UpdatePublicationRequest(
                    SiteName: "Default Web Site",
                    VirtualPath: "/ib",
                    PlatformVersion: "8.3.23.1865",
                    PhysicalPathOverride: null)),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            clock,
            CancellationToken.None);

        update.Result.Should().BeOfType<Ok<InfobaseDetailResponse>>();
    }
}
