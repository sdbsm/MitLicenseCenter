using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-151 — оптимистическая блокировка Infobase и Publication через rowversion-токен
// (зеркаль TenantConcurrencyTests/MLC-136). EF InMemory НЕ генерирует rowversion и не
// воспроизводит конкурентность, поэтому конфликт эмулируем перехватчиком, бросающим
// DbUpdateConcurrencyException на SaveChanges — ровно как SQL Server при
// UPDATE ... WHERE RowVersion = @original, затронувшем 0 строк. Update-эндпоинты с непустым
// токеном должны поймать его и вернуть 409 (а не 500); при null — backward-compat (успех).
public sealed class InfobaseConcurrencyTests
{
    private static readonly TimeProvider Clock =
        TestHelpers.FixedClock(new DateTime(2026, 6, 14, 12, 0, 0, DateTimeKind.Utc));

    private static readonly byte[] StaleToken = [1, 2, 3, 4, 5, 6, 7, 8];

    private sealed record Seed(Guid TenantId, Guid InfobaseId, Guid PublicationId);

    private static async Task<Seed> SeedAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        var now = Clock.GetUtcNow().UtcDateTime;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = now,
        });
        db.Infobases.Add(new Infobase
        {
            Id = infobaseId,
            TenantId = tenantId,
            Name = "Acme BP",
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseName = "acme_bp",
            Status = InfobaseStatus.Active,
            CreatedAt = now,
        });
        db.Publications.Add(new Publication
        {
            Id = publicationId,
            InfobaseId = infobaseId,
            SiteName = "Default Web Site",
            VirtualPath = "/MyPub",
            PlatformVersion = "8.3.23.1865",
            Source = PublicationSource.Webinst,
            LastCheckStatus = PublicationPublishStatus.Unknown,
            CreatedAt = now,
        });
        await db.SaveChangesAsync();
        return new Seed(tenantId, infobaseId, publicationId);
    }

    private static UpdateInfobaseRequest InfobaseRequest(byte[]? rowVersion = null, byte[]? publicationRowVersion = null) =>
        new(
            "Acme BP Renamed",
            Guid.NewGuid(),
            "acme_bp",
            InfobaseStatus.Active,
            new UpdatePublicationRequest("Default Web Site", "/MyPub", "8.3.23.1865", null, publicationRowVersion),
            rowVersion);

    // ── PUT /infobases/{id} — aggregate-апдейт (корень + вложенная публикация) ──────────

    [Fact]
    public async Task Infobase_update_with_stale_rowversion_maps_to_409_INFOBASE_CONCURRENCY_CONFLICT()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            new DbUpdateConcurrencyException("Database operation expected to affect 1 row(s) but actually affected 0 row(s)."));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var seed = await SeedAsync(db);

        interceptor.Armed = true;
        var result = await InfobasesEndpoints.UpdateAsync(
            seed.InfobaseId,
            InfobaseRequest(rowVersion: StaleToken),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("admin"),
            Clock,
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.InfobaseConcurrencyConflict);
    }

    [Fact]
    public async Task Infobase_update_with_stale_publication_rowversion_maps_to_409()
    {
        // Вложенный Publication.RowVersion устарел — тот же aggregate-апдейт ловит конфликт.
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            new DbUpdateConcurrencyException("Database operation expected to affect 1 row(s) but actually affected 0 row(s)."));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var seed = await SeedAsync(db);

        interceptor.Armed = true;
        var result = await InfobasesEndpoints.UpdateAsync(
            seed.InfobaseId,
            InfobaseRequest(publicationRowVersion: StaleToken),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("admin"),
            Clock,
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.InfobaseConcurrencyConflict);
    }

    [Fact]
    public async Task Infobase_update_without_rowversion_succeeds_backward_compatible()
    {
        // rowVersion=null (старый клиент / InMemory) — апдейт проходит как раньше.
        await using var db = TestHelpers.NewInMemoryDb();
        var seed = await SeedAsync(db);

        var result = await InfobasesEndpoints.UpdateAsync(
            seed.InfobaseId,
            InfobaseRequest(),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("admin"),
            Clock,
            CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<InfobaseDetailResponse>>().Subject;
        ok.Value!.Infobase.Name.Should().Be("Acme BP Renamed");
        // Контракт ответа несёт RowVersion обеих сущностей (под InMemory — null).
        ok.Value.Infobase.RowVersion.Should().BeNull();
        ok.Value.Publication.RowVersion.Should().BeNull();
    }

    // ── PUT /publications/{id} — самостоятельный путь правки публикации (вариант (b)) ────

    [Fact]
    public async Task Publication_update_with_stale_rowversion_maps_to_409_PUBLICATION_CONCURRENCY_CONFLICT()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            new DbUpdateConcurrencyException("Database operation expected to affect 1 row(s) but actually affected 0 row(s)."));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var seed = await SeedAsync(db);

        interceptor.Armed = true;
        var result = await PublicationsEndpoints.UpdateAsync(
            seed.PublicationId,
            new UpdatePublicationRequest("Default Web Site", "/Renamed", "8.3.23.1865", null, StaleToken),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("admin"),
            Clock,
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.PublicationConcurrencyConflict);
    }

    [Fact]
    public async Task Publication_update_without_rowversion_succeeds_backward_compatible()
    {
        await using var db = TestHelpers.NewInMemoryDb();
        var seed = await SeedAsync(db);

        var result = await PublicationsEndpoints.UpdateAsync(
            seed.PublicationId,
            new UpdatePublicationRequest("Default Web Site", "/Renamed", "8.3.23.1865", null),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("admin"),
            Clock,
            CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<PublicationResponse>>().Subject;
        ok.Value!.VirtualPath.Should().Be("/Renamed");
        ok.Value.RowVersion.Should().BeNull();
    }
}
