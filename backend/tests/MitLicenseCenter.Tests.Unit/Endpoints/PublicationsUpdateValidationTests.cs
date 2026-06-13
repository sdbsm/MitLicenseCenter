using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// BE-09 (MLC-120) — поведенческие тесты валидации PUT /api/v1/publications/{id}
// (PublicationsEndpoints.UpdateAsync, Admin). Это единственный runtime-барьер: на
// minimal API DataAnnotations [StringLength] в рантайме НЕ срабатывают (гоча MLC-118),
// реальная проверка идёт через InfobaseValidationRules.AppendPublicationFieldErrors.
// Стиль Stage-2: зовём internal static handler напрямую, без WebApplicationFactory.
// Ассерты привязаны к фактическим предикатам InfobaseValidationRules:
//   SiteNameMaxLength=200, VirtualPathMaxLength=200, PhysicalPathMaxLength=260;
//   VirtualPath: запрет «\», «..», должен начинаться с «/», без пробелов;
//   PhysicalPathOverride: должен быть абсолютным, без «..», без «; = "».
public sealed class PublicationsUpdateValidationTests
{
    [Fact]
    public async Task Update_valid_fields_returns_200_with_response()
    {
        await using var ctx = await TestContext.NewAsync();

        var result = await PublicationsEndpoints_Invoke(ctx,
            new UpdatePublicationRequest("Default Web Site", "/acme", "8.3.23.1865", @"C:\pub\acme"));

        var ok = result.Result.Should().BeOfType<Ok<PublicationResponse>>().Subject;
        ok.Value!.SiteName.Should().Be("Default Web Site");
        ok.Value.VirtualPath.Should().Be("/acme");
        ok.Value.PlatformVersion.Should().Be("8.3.23.1865");
        ok.Value.PhysicalPathOverride.Should().Be(@"C:\pub\acme");

        // Запись действительно мутирована в БД.
        ctx.Db.Publications.Single().VirtualPath.Should().Be("/acme");
        ctx.Audit.Entries.Should().ContainSingle(e =>
            e.Action == AuditActionType.PublicationUpdated);
    }

    [Fact]
    public async Task Update_valid_without_physical_path_override_returns_200_and_nulls_it()
    {
        await using var ctx = await TestContext.NewAsync();

        var result = await PublicationsEndpoints_Invoke(ctx,
            new UpdatePublicationRequest("Default Web Site", "/acme", "8.3.23.1865", null));

        result.Result.Should().BeOfType<Ok<PublicationResponse>>();
        ctx.Db.Publications.Single().PhysicalPathOverride.Should().BeNull();
    }

    // ── длина: 400 ValidationProblem (НЕ 500) ────────────────────────────────────────

    [Fact]
    public async Task Update_sitename_over_max_returns_400_with_SiteName_error()
    {
        await using var ctx = await TestContext.NewAsync();
        var tooLong = new string('s', InfobaseValidationRules.SiteNameMaxLength + 1);

        var result = await PublicationsEndpoints_Invoke(ctx,
            new UpdatePublicationRequest(tooLong, "/acme", "8.3.23.1865", null));

        var vp = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        vp.ProblemDetails.Errors.Should().ContainKey("SiteName");
        ctx.Audit.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_virtualpath_over_max_returns_400_with_VirtualPath_error()
    {
        await using var ctx = await TestContext.NewAsync();
        // "/" + (max) символов → длиннее лимита, но начинается с «/» и без пробелов,
        // чтобы сработала именно ветка длины.
        var tooLong = "/" + new string('a', InfobaseValidationRules.VirtualPathMaxLength);

        var result = await PublicationsEndpoints_Invoke(ctx,
            new UpdatePublicationRequest("Default Web Site", tooLong, "8.3.23.1865", null));

        var vp = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        vp.ProblemDetails.Errors.Should().ContainKey("VirtualPath");
    }

    [Fact]
    public async Task Update_physical_path_over_max_returns_400_with_PhysicalPathOverride_error()
    {
        await using var ctx = await TestContext.NewAsync();
        var tooLong = @"C:\" + new string('p', InfobaseValidationRules.PhysicalPathMaxLength);

        var result = await PublicationsEndpoints_Invoke(ctx,
            new UpdatePublicationRequest("Default Web Site", "/acme", "8.3.23.1865", tooLong));

        var vp = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        vp.ProblemDetails.Errors.Should().ContainKey("PhysicalPathOverride");
    }

    // ── метасимволы / формат: 400 ValidationProblem ──────────────────────────────────

    [Fact]
    public async Task Update_virtualpath_with_backslash_returns_400()
    {
        await using var ctx = await TestContext.NewAsync();

        // «\» запрещён в VirtualPath (IsSafeVirtualPath). Начинается с «/», без пробелов,
        // без «..» → провалит именно ветку IsSafeVirtualPath.
        var result = await PublicationsEndpoints_Invoke(ctx,
            new UpdatePublicationRequest("Default Web Site", "/acme\\bad", "8.3.23.1865", null));

        var vp = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        vp.ProblemDetails.Errors.Should().ContainKey("VirtualPath");
    }

    [Fact]
    public async Task Update_virtualpath_with_dotdot_returns_400()
    {
        await using var ctx = await TestContext.NewAsync();

        var result = await PublicationsEndpoints_Invoke(ctx,
            new UpdatePublicationRequest("Default Web Site", "/acme/../etc", "8.3.23.1865", null));

        var vp = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        vp.ProblemDetails.Errors.Should().ContainKey("VirtualPath");
    }

    [Fact]
    public async Task Update_physical_path_not_absolute_returns_400()
    {
        await using var ctx = await TestContext.NewAsync();

        // Относительный путь отвергается (Path.IsPathFullyQualified == false).
        var result = await PublicationsEndpoints_Invoke(ctx,
            new UpdatePublicationRequest("Default Web Site", "/acme", "8.3.23.1865", @"relative\path"));

        var vp = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        vp.ProblemDetails.Errors.Should().ContainKey("PhysicalPathOverride");
    }

    [Fact]
    public async Task Update_physical_path_with_forbidden_metachar_returns_400()
    {
        await using var ctx = await TestContext.NewAsync();

        // Абсолютный путь, но содержит запрещённый «;» (PhysicalPathForbiddenChars = «; = "»).
        var result = await PublicationsEndpoints_Invoke(ctx,
            new UpdatePublicationRequest("Default Web Site", "/acme", "8.3.23.1865", @"C:\pub;DROP"));

        var vp = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        vp.ProblemDetails.Errors.Should().ContainKey("PhysicalPathOverride");
    }

    [Fact]
    public async Task Update_not_found_returns_404()
    {
        await using var ctx = await TestContext.NewAsync();

        var result = await PublicationsEndpoints.UpdateAsync(
            Guid.NewGuid(),
            new UpdatePublicationRequest("Default Web Site", "/acme", "8.3.23.1865", null),
            ctx.Db, ctx.Audit, TestHelpers.NewHttpContext("admin"), TimeProvider.System, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    // Вызов handler'а для существующей публикации фикстуры.
    private static Task<Results<Ok<PublicationResponse>, NotFound, ValidationProblem>> PublicationsEndpoints_Invoke(
        TestContext ctx, UpdatePublicationRequest request) =>
        PublicationsEndpoints.UpdateAsync(
            ctx.PublicationId, request, ctx.Db, ctx.Audit,
            TestHelpers.NewHttpContext("admin"), TimeProvider.System, CancellationToken.None);

    private sealed class TestContext : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid InfobaseId { get; } = Guid.NewGuid();
        public Guid PublicationId { get; } = Guid.NewGuid();

        public TestHelpers.CapturingAuditLogger Audit { get; } = new();
        public AppDbContext Db { get; } = TestHelpers.NewInMemoryDb();

        public static async Task<TestContext> NewAsync()
        {
            var ctx = new TestContext();
            ctx.Db.Tenants.Add(new Tenant
            {
                Id = ctx.TenantId,
                Name = "Acme",
                MaxConcurrentLicenses = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            ctx.Db.Infobases.Add(new Infobase
            {
                Id = ctx.InfobaseId,
                TenantId = ctx.TenantId,
                Name = "Acme BP",
                ClusterInfobaseId = Guid.NewGuid(),
                DatabaseName = "acme_bp",
                Status = InfobaseStatus.Active,
                CreatedAt = DateTime.UtcNow,
            });
            ctx.Db.Publications.Add(new Publication
            {
                Id = ctx.PublicationId,
                InfobaseId = ctx.InfobaseId,
                SiteName = "Default Web Site",
                VirtualPath = "/MyPub",
                PlatformVersion = "8.3.23.1865",
                Source = PublicationSource.Webinst,
                LastCheckStatus = PublicationPublishStatus.Unknown,
                CreatedAt = DateTime.UtcNow,
            });
            await ctx.Db.SaveChangesAsync();
            return ctx;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
