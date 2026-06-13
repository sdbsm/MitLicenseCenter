using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-119 (BE-11) — смена MaxConcurrentLicenses пишет ДОПОЛНИТЕЛЬНОЕ событие LimitChanged
// (старое→новое значение), отдельно от общего TenantUpdated. Без изменения лимита
// LimitChanged НЕ пишется (только TenantUpdated).
public sealed class TenantLimitChangedAuditTests
{
    private static readonly TimeProvider Clock =
        TestHelpers.FixedClock(new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc));

    private static Tenant SeededTenant(string name = "Acme", int limit = 10) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxConcurrentLicenses = limit,
        IsActive = true,
        CreatedAt = Clock.GetUtcNow().UtcDateTime,
    };

    [Fact]
    public async Task Update_changing_limit_writes_TenantUpdated_and_LimitChanged_with_old_new()
    {
        await using var db = TestHelpers.NewInMemoryDb();
        var tenant = SeededTenant(limit: 10);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        var result = await TenantsEndpoints.UpdateAsync(
            tenant.Id, new UpdateTenantRequest(tenant.Name, 25, IsActive: true),
            db, audit, TestHelpers.NewHttpContext("admin"), Clock, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<TenantResponse>>();

        audit.Entries.Should().Contain(e => e.Action == AuditActionType.TenantUpdated);
        var limitChanged = audit.Entries.Should()
            .ContainSingle(e => e.Action == AuditActionType.LimitChanged).Subject;
        limitChanged.TenantId.Should().Be(tenant.Id);
        limitChanged.Description.Should().Be(
            "Лимит лицензий клиента «Acme» изменён с 10 на 25 администратором admin.");
    }

    [Fact]
    public async Task Update_without_limit_change_writes_only_TenantUpdated()
    {
        await using var db = TestHelpers.NewInMemoryDb();
        var tenant = SeededTenant(name: "Acme", limit: 10);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var audit = new TestHelpers.CapturingAuditLogger();
        // Меняем имя, лимит тот же (10).
        var result = await TenantsEndpoints.UpdateAsync(
            tenant.Id, new UpdateTenantRequest("Acme Renamed", 10, IsActive: true),
            db, audit, TestHelpers.NewHttpContext("admin"), Clock, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<TenantResponse>>();

        audit.Entries.Should().Contain(e => e.Action == AuditActionType.TenantUpdated);
        audit.Entries.Should().NotContain(e => e.Action == AuditActionType.LimitChanged,
            "без фактической смены лимита LimitChanged не пишется");
    }
}
