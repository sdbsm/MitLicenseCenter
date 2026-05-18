using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// Guard, обещанный в PR 2.2 TODO: tenant с инфобазой не удаляется → 409
// с machine-readable code TENANT_HAS_INFOBASES. Без инфобаз — нормальное 204.
public sealed class TenantDeletionGuardTests
{
    [Fact]
    public async Task Delete_returns_Conflict_TENANT_HAS_INFOBASES_when_tenant_has_infobase()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.Infobases.Add(new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Acme BP",
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseServer = "sql.local",
            DatabaseName = "acme_bp",
            Status = InfobaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        var result = await TenantsEndpoints.DeleteAsync(
            tenant.Id,
            db,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.TenantHasInfobases);
        audit.Entries.Should().BeEmpty("аудит не пишется при отказе guard'ом");
    }

    [Fact]
    public async Task Delete_succeeds_when_tenant_has_no_infobases()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        var result = await TenantsEndpoints.DeleteAsync(
            tenant.Id,
            db,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.TenantDeleted);
    }
}
