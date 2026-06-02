using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-015 — точечная проверка занятости базы кластера. Заменяет выгрузку всего списка
// инфобаз во фронтовой форме: эндпоинт отвечает, привязана ли база к клиенту и к какому.
public sealed class ClusterIdAvailabilityTests
{
    private static Tenant SeedTenant(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxConcurrentLicenses = 10,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static Infobase SeedInfobase(Guid tenantId, Guid clusterInfobaseId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = "База",
        ClusterInfobaseId = clusterInfobaseId,
        DatabaseServer = "sql.local",
        DatabaseName = "ib",
        Status = InfobaseStatus.Active,
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task Taken_cluster_id_returns_owner_tenant_name()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = SeedTenant("Acme");
        var clusterId = Guid.NewGuid();
        db.Tenants.Add(acme);
        db.Infobases.Add(SeedInfobase(acme.Id, clusterId));
        await db.SaveChangesAsync();

        var result = await InfobasesEndpoints.ClusterIdAvailabilityAsync(
            db, clusterId, excludeId: null, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<ClusterIdAvailabilityResponse>>().Subject;
        ok.Value!.Taken.Should().BeTrue();
        ok.Value.TakenByTenantName.Should().Be("Acme");
    }

    [Fact]
    public async Task Free_cluster_id_returns_not_taken()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = SeedTenant("Acme");
        db.Tenants.Add(acme);
        db.Infobases.Add(SeedInfobase(acme.Id, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var result = await InfobasesEndpoints.ClusterIdAvailabilityAsync(
            db, Guid.NewGuid(), excludeId: null, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<ClusterIdAvailabilityResponse>>().Subject;
        ok.Value!.Taken.Should().BeFalse();
        ok.Value.TakenByTenantName.Should().BeNull();
    }

    [Fact]
    public async Task Own_infobase_is_excluded_via_excludeId()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var acme = SeedTenant("Acme");
        var clusterId = Guid.NewGuid();
        var infobase = SeedInfobase(acme.Id, clusterId);
        db.Tenants.Add(acme);
        db.Infobases.Add(infobase);
        await db.SaveChangesAsync();

        // Редактирование собственной базы: её же ClusterInfobaseId не считается занятым.
        var result = await InfobasesEndpoints.ClusterIdAvailabilityAsync(
            db, clusterId, excludeId: infobase.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<ClusterIdAvailabilityResponse>>().Subject;
        ok.Value!.Taken.Should().BeFalse();
        ok.Value.TakenByTenantName.Should().BeNull();
    }
}
