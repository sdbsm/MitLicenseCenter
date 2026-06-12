using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// DELETE инфобазы — cascade-удаление публикации в той же транзакции. Аудит — две записи
// без снятия из IIS; при unpublishFromIis=true (MLC-113, UX-43) добавляется снятие
// публикации через webinst -delete ДО удаления, с откатом в 409 при сбое.
public sealed class InfobaseCascadeDeleteTests
{
    private static (AppDbContext Db, Tenant Tenant, Infobase Infobase, Publication Publication) Seed()
    {
        var db = TestHelpers.NewInMemoryDb();
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
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.Infobases.Add(infobase);
        db.Publications.Add(publication);
        db.SaveChanges();
        return (db, tenant, infobase, publication);
    }

    [Fact]
    public async Task Delete_removes_both_Infobase_and_Publication_and_writes_two_audit_entries()
    {
        var (db, tenant, infobase, _) = Seed();
        using var _db = db;
        var webinst = Substitute.For<IWebinstPublisher>();

        var audit = new TestHelpers.CapturingAuditLogger();
        var result = await InfobasesEndpoints.DeleteAsync(
            infobase.Id,
            db,
            audit,
            webinst,
            TestHelpers.NewHttpContext(),
            CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();

        (await db.Infobases.CountAsync()).Should().Be(0);
        (await db.Publications.CountAsync()).Should().Be(0, "Publication уходит каскадом вместе с Infobase");

        audit.Entries.Should().HaveCount(2);
        audit.Entries.Should().Contain(e => e.Action == AuditActionType.PublicationDeleted && e.TenantId == tenant.Id);
        audit.Entries.Should().Contain(e => e.Action == AuditActionType.InfobaseDeleted && e.TenantId == tenant.Id);

        // unpublishFromIis по умолчанию false — webinst не зовём (прежнее поведение).
        await webinst.DidNotReceiveWithAnyArgs().UnpublishAsync(default!, default!, default);
    }

    [Fact]
    public async Task Delete_with_unpublish_success_calls_webinst_and_writes_three_audit_entries()
    {
        var (db, tenant, infobase, _) = Seed();
        using var _db = db;
        var webinst = Substitute.For<IWebinstPublisher>();
        webinst.UnpublishAsync(Arg.Any<Publication>(), Arg.Any<Infobase>(), Arg.Any<CancellationToken>())
            .Returns(WebinstResult.Ok());

        var audit = new TestHelpers.CapturingAuditLogger();
        var result = await InfobasesEndpoints.DeleteAsync(
            infobase.Id,
            db,
            audit,
            webinst,
            TestHelpers.NewHttpContext(),
            CancellationToken.None,
            unpublishFromIis: true);

        result.Result.Should().BeOfType<NoContent>();

        await webinst.Received(1).UnpublishAsync(Arg.Any<Publication>(), Arg.Any<Infobase>(), Arg.Any<CancellationToken>());

        (await db.Infobases.CountAsync()).Should().Be(0);
        (await db.Publications.CountAsync()).Should().Be(0);

        audit.Entries.Should().HaveCount(3);
        audit.Entries.Should().Contain(e => e.Action == AuditActionType.PublicationUnpublished && e.TenantId == tenant.Id);
        audit.Entries.Should().Contain(e => e.Action == AuditActionType.PublicationDeleted);
        audit.Entries.Should().Contain(e => e.Action == AuditActionType.InfobaseDeleted);
    }

    [Fact]
    public async Task Delete_with_unpublish_webinst_failure_returns_409_and_deletes_nothing()
    {
        var (db, _, infobase, _) = Seed();
        using var _db = db;
        var webinst = Substitute.For<IWebinstPublisher>();
        webinst.UnpublishAsync(Arg.Any<Publication>(), Arg.Any<Infobase>(), Arg.Any<CancellationToken>())
            .Returns(WebinstResult.Failed("Не удалось снять публикацию инфобазы через webinst."));

        var audit = new TestHelpers.CapturingAuditLogger();
        var result = await InfobasesEndpoints.DeleteAsync(
            infobase.Id,
            db,
            audit,
            webinst,
            TestHelpers.NewHttpContext(),
            CancellationToken.None,
            unpublishFromIis: true);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.UnpublishFailed);

        // Защита от сиротства: ничего не удалено, ни одной аудит-записи.
        (await db.Infobases.CountAsync()).Should().Be(1, "при сбое снятия строки не удаляются");
        (await db.Publications.CountAsync()).Should().Be(1);
        audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_without_unpublish_does_not_call_webinst()
    {
        var (db, _, infobase, _) = Seed();
        using var _db = db;
        var webinst = Substitute.For<IWebinstPublisher>();

        var audit = new TestHelpers.CapturingAuditLogger();
        var result = await InfobasesEndpoints.DeleteAsync(
            infobase.Id,
            db,
            audit,
            webinst,
            TestHelpers.NewHttpContext(),
            CancellationToken.None,
            unpublishFromIis: false);

        result.Result.Should().BeOfType<NoContent>();
        await webinst.DidNotReceiveWithAnyArgs().UnpublishAsync(default!, default!, default);
        audit.Entries.Should().NotContain(e => e.Action == AuditActionType.PublicationUnpublished);
    }
}
