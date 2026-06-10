using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// Список клиентов отдаёт InfobaseCount — число баз, привязанных к каждому клиенту
// (коррелированный COUNT в проекции). Линза «какие базы у клиента» опирается на него.
public sealed class TenantInfobaseCountTests
{
    [Fact]
    public async Task List_reports_infobase_count_per_tenant()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var withTwo = NewTenant("Acme");
        var withNone = NewTenant("Globex");
        db.Tenants.AddRange(withTwo, withNone);
        db.Infobases.AddRange(
            NewInfobase(withTwo.Id, "Acme BP"),
            NewInfobase(withTwo.Id, "Acme ZUP"));
        await db.SaveChangesAsync();

        var result = await TenantsEndpoints.ListAsync(db, page: 1, pageSize: 50, CancellationToken.None);

        var items = result.Value!.Items;
        items.Single(t => t.Id == withTwo.Id).InfobaseCount.Should().Be(2);
        items.Single(t => t.Id == withNone.Id).InfobaseCount.Should().Be(0);
    }

    private static Tenant NewTenant(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxConcurrentLicenses = 10,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static Infobase NewInfobase(Guid tenantId, string name) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = name,
        ClusterInfobaseId = Guid.NewGuid(),
        DatabaseName = name.ToLowerInvariant().Replace(' ', '_'),
        Status = InfobaseStatus.Active,
        CreatedAt = DateTime.UtcNow,
    };
}
