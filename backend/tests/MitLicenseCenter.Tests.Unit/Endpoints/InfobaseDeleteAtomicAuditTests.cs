using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-119 (BE-01, второй экземпляр) — записи PublicationDeleted/InfobaseDeleted коммитятся
// ТЕМ ЖЕ SaveChanges, что и удаление строк (enlist в общий контекст), а не своим SaveChanges
// ДО удаления. Поэтому при сбое удаления в БД не остаётся ложных «удалена»: либо «удаление +
// обе записи», либо «ничего».
//
// Прогон на реальном контексте с продакшн-AuditLogger поверх ТОГО ЖЕ db, чтобы enlist шёл в
// общий tracked-контекст. ThrowOnSaveInterceptor взводится после сидинга, перед вызовом
// эндпоинта — стреляет ровно на SaveChanges операции удаления. unpublishFromIis=false, чтобы
// не дёргать webinst (PublicationUnpublished — отдельный action-first путь, здесь не участвует).
public sealed class InfobaseDeleteAtomicAuditTests
{
    private static readonly TimeProvider Clock =
        TestHelpers.FixedClock(new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc));

    private static (Tenant Tenant, Infobase Infobase, Publication Publication) Seed(AppDbContext db)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
        };
        var infobase = new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Acme BP",
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseName = "acme_bp",
            Status = InfobaseStatus.Active,
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
        };
        var publication = new Publication
        {
            Id = Guid.NewGuid(),
            InfobaseId = infobase.Id,
            SiteName = "Default Web Site",
            VirtualPath = "/acme-bp",
            PlatformVersion = "8.3.23.1865",
            CreatedAt = Clock.GetUtcNow().UtcDateTime,
        };
        db.Tenants.Add(tenant);
        db.Infobases.Add(infobase);
        db.Publications.Add(publication);
        db.SaveChanges();
        return (tenant, infobase, publication);
    }

    [Fact]
    public async Task Delete_failure_leaves_neither_rows_removed_nor_delete_audit()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            new DbUpdateException("симулированный сбой удаления"));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var (_, infobase, _) = Seed(db);
        var webinst = Substitute.For<IWebinstPublisher>();

        // Взводим интерсептор — следующий SaveChanges (удаление) бросит.
        interceptor.Armed = true;

        var audit = new AuditLogger(db, Clock);
        var act = async () => await InfobasesEndpoints.DeleteAsync(
            infobase.Id,
            db,
            audit,
            webinst,
            TestHelpers.NewHttpContext(),
            CancellationToken.None,
            unpublishFromIis: false);

        await act.Should().ThrowAsync<DbUpdateException>();

        // Снимаем взвод и читаем напрямую из БД: SaveChanges не прошёл, значит ни удаление,
        // ни enlist'ленные аудит-записи не закоммичены (InMemory отражает только saved-состояние).
        interceptor.Armed = false;
        (await db.Infobases.AsNoTracking().AnyAsync(x => x.Id == infobase.Id))
            .Should().BeTrue("при сбое удаления инфобаза остаётся на месте");
        (await db.Publications.AsNoTracking().AnyAsync(p => p.InfobaseId == infobase.Id))
            .Should().BeTrue("при сбое удаления публикация остаётся на месте");
        (await db.AuditLogs.AsNoTracking().AnyAsync(a => a.ActionType == AuditActionType.InfobaseDeleted))
            .Should().BeFalse("ложной InfobaseDeleted-записи при сбое удаления быть не должно");
        (await db.AuditLogs.AsNoTracking().AnyAsync(a => a.ActionType == AuditActionType.PublicationDeleted))
            .Should().BeFalse("ложной PublicationDeleted-записи при сбое удаления быть не должно");

        await webinst.DidNotReceiveWithAnyArgs().UnpublishAsync(default!, default!, default);
    }

    [Fact]
    public async Task Delete_success_writes_exactly_one_each_and_removes_rows()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var (_, infobase, _) = Seed(db);
        var webinst = Substitute.For<IWebinstPublisher>();

        var audit = new AuditLogger(db, Clock);
        var result = await InfobasesEndpoints.DeleteAsync(
            infobase.Id,
            db,
            audit,
            webinst,
            TestHelpers.NewHttpContext(),
            CancellationToken.None,
            unpublishFromIis: false);

        result.Result.Should().BeOfType<NoContent>();
        (await db.Infobases.AsNoTracking().AnyAsync(x => x.Id == infobase.Id))
            .Should().BeFalse("успешное удаление сносит инфобазу");
        (await db.Publications.AsNoTracking().AnyAsync(p => p.InfobaseId == infobase.Id))
            .Should().BeFalse("публикация уходит вместе с инфобазой");

        (await db.AuditLogs.AsNoTracking()
            .CountAsync(a => a.ActionType == AuditActionType.InfobaseDeleted))
            .Should().Be(1, "ровно одна InfobaseDeleted на успешный DELETE");
        (await db.AuditLogs.AsNoTracking()
            .CountAsync(a => a.ActionType == AuditActionType.PublicationDeleted))
            .Should().Be(1, "ровно одна PublicationDeleted, т.к. публикация была");

        await webinst.DidNotReceiveWithAnyArgs().UnpublishAsync(default!, default!, default);
    }
}
