using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// DELETE инфобазы — cascade-удаление публикации в той же транзакции
// (Stage 2: только запись в БД, IIS-unpublish — Stage 3). Аудит — две записи.
public sealed class InfobaseCascadeDeleteTests
{
    [Fact]
    public async Task Delete_removes_both_Infobase_and_Publication_and_writes_two_audit_entries()
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
        var infobase = new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Acme BP",
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseServer = "sql.local",
            DatabaseName = "acme_bp",
            Status = InfobaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        var publication = new Publication
        {
            Id = Guid.NewGuid(),
            InfobaseId = infobase.Id,
            SiteName = "Default Web Site",
            VirtualPath = "/acme-bp",
            PlatformVersion = "8.3.23.1865",
            EnableOData = false,
            EnableHttpServices = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.Infobases.Add(infobase);
        db.Publications.Add(publication);
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        var result = await InfobasesEndpoints.DeleteAsync(
            infobase.Id,
            db,
            audit,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();

        (await db.Infobases.CountAsync()).Should().Be(0);
        (await db.Publications.CountAsync()).Should().Be(0, "Publication уходит каскадом вместе с Infobase");

        audit.Entries.Should().HaveCount(2);
        audit.Entries.Should().Contain(e => e.Action == AuditActionType.PublicationDeleted && e.TenantId == tenant.Id);
        audit.Entries.Should().Contain(e => e.Action == AuditActionType.InfobaseDeleted && e.TenantId == tenant.Id);
    }
}
