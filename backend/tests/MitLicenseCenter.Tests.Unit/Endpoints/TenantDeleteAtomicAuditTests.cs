using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-119 (BE-01) — аудит TenantDeleted коммитится ТЕМ ЖЕ SaveChanges, что и удаление
// (enlist в общий контекст), а не своим SaveChanges ДО удаления. Поэтому при сбое удаления
// в БД не остаётся ложной аудит-записи: либо «удаление + запись», либо «ничего».
//
// Прогон на реальном контексте (InMemory достаточно: проверяется одна транзакция/один
// SaveChanges, а не FK-поведение) с продакшн-AuditLogger поверх ТОГО ЖЕ db, чтобы enlist
// шёл в общий tracked-контекст. ThrowOnSaveInterceptor взводится после сидинга, перед
// вызовом эндпоинта — стреляет ровно на SaveChanges операции удаления.
public sealed class TenantDeleteAtomicAuditTests
{
    private static readonly TimeProvider Clock =
        TestHelpers.FixedClock(new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task Delete_failure_leaves_neither_tenant_removed_nor_TenantDeleted_audit()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            new DbUpdateException("симулированный сбой удаления"));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Взводим интерсептор — следующий SaveChanges (удаление) бросит.
        interceptor.Armed = true;

        var audit = new AuditLogger(db, Clock);
        var act = async () => await TenantsEndpoints.DeleteAsync(
            tenant.Id, db, audit, TestHelpers.NewHttpContext(), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();

        // Снимаем взвод и читаем напрямую из БД: SaveChanges не прошёл, значит ни удаление,
        // ни enlist'ленная аудит-запись не закоммичены (InMemory отражает только saved-состояние).
        interceptor.Armed = false;
        (await db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenant.Id))
            .Should().BeTrue("при сбое удаления tenant остаётся");
        (await db.AuditLogs.AsNoTracking().AnyAsync(a => a.ActionType == AuditActionType.TenantDeleted))
            .Should().BeFalse("ложной TenantDeleted-записи при сбое удаления быть не должно");
    }

    [Fact]
    public async Task Delete_success_writes_exactly_one_TenantDeleted_and_removes_tenant()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var audit = new AuditLogger(db, Clock);
        var result = await TenantsEndpoints.DeleteAsync(
            tenant.Id, db, audit, TestHelpers.NewHttpContext(), CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();
        (await db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenant.Id))
            .Should().BeFalse("успешное удаление сносит tenant");
        var entries = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ActionType == AuditActionType.TenantDeleted).ToListAsync();
        entries.Should().ContainSingle("ровно одна TenantDeleted на успешный DELETE");
    }
}
