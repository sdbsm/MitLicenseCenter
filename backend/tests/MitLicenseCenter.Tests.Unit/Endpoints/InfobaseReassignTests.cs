using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// Перенос базы другому клиенту: меняет TenantId, пишет аудит InfobaseReassigned,
// а при коллизии имени у целевого клиента отвечает 409 INFOBASE_NAME_TAKEN_IN_TARGET.
public sealed class InfobaseReassignTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Reassign_moves_base_to_target_tenant_and_audits()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var source = NewTenant("Acme");
        var target = NewTenant("Globex");
        db.Tenants.AddRange(source, target);
        var ib = NewInfobase(source.Id, "Бухгалтерия");
        db.Infobases.Add(ib);
        db.Publications.Add(NewPublication(ib.Id));
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        var result = await InfobasesEndpoints.ReassignAsync(
            ib.Id,
            new ReassignInfobaseRequest(target.Id),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            TestHelpers.FixedClock(Now),
            CancellationToken.None);

        result.Result.Should().BeOfType<Ok<InfobaseDetailResponse>>();
        (await db.Infobases.AsNoTracking().FirstAsync(x => x.Id == ib.Id)).TenantId.Should().Be(target.Id);
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.InfobaseReassigned && e.TenantId == target.Id);
    }

    [Fact]
    public async Task Reassign_returns_Conflict_when_name_taken_in_target()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var source = NewTenant("Acme");
        var target = NewTenant("Globex");
        db.Tenants.AddRange(source, target);
        var ib = NewInfobase(source.Id, "Бухгалтерия");
        db.Infobases.Add(ib);
        db.Publications.Add(NewPublication(ib.Id));
        // У целевого клиента уже есть одноимённая база.
        db.Infobases.Add(NewInfobase(target.Id, "Бухгалтерия"));
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        var result = await InfobasesEndpoints.ReassignAsync(
            ib.Id,
            new ReassignInfobaseRequest(target.Id),
            db,
            audit,
            TestHelpers.NewHttpContext(),
            TestHelpers.FixedClock(Now),
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.InfobaseNameTakenInTarget);
        (await db.Infobases.AsNoTracking().FirstAsync(x => x.Id == ib.Id)).TenantId.Should().Be(source.Id);
        audit.Entries.Should().BeEmpty("аудит не пишется при отказе guard'ом");
    }

    private static Tenant NewTenant(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxConcurrentLicenses = 10,
        IsActive = true,
        CreatedAt = Now,
    };

    private static Infobase NewInfobase(Guid tenantId, string name) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = name,
        ClusterInfobaseId = Guid.NewGuid(),
        DatabaseServer = "sql.local",
        DatabaseName = "db",
        Status = InfobaseStatus.Active,
        CreatedAt = Now,
    };

    private static Publication NewPublication(Guid infobaseId) => new()
    {
        Id = Guid.NewGuid(),
        InfobaseId = infobaseId,
        SiteName = "Default Web Site",
        VirtualPath = "/db",
        PlatformVersion = "8.3.23.1865",
        CreatedAt = Now,
    };
}
