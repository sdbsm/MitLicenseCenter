using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Discovery;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-045: операции публикации — check (read-only), publish (webinst), change-platform.
public sealed class PublicationsOperationsTests
{
    // ── check ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_refreshes_and_returns_status()
    {
        await using var ctx = await TestContext.NewAsync();
        ctx.Iis.ReadActualStateAsync(Arg.Any<Publication>(), Arg.Any<CancellationToken>())
            .Returns(new PublicationActualState(true, true, true, "8.3.23.1865", null));

        var result = await PublicationsEndpoints.CheckAsync(ctx.PublicationId, ctx.Db, ctx.Job, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<PublicationStatusResponse>>().Subject;
        ok.Value!.Status.Should().Be(PublicationPublishStatus.Published);
    }

    [Fact]
    public async Task Check_not_found_returns_404()
    {
        await using var ctx = await TestContext.NewAsync();
        var result = await PublicationsEndpoints.CheckAsync(Guid.NewGuid(), ctx.Db, ctx.Job, CancellationToken.None);
        result.Result.Should().BeOfType<NotFound>();
    }

    // ── publish ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Publish_success_sets_source_and_writes_212_audit()
    {
        await using var ctx = await TestContext.NewAsync(source: PublicationSource.Unknown);
        ctx.Webinst.PublishAsync(Arg.Any<Publication>(), Arg.Any<Infobase>(), Arg.Any<CancellationToken>())
            .Returns(WebinstResult.Ok());
        ctx.Iis.ReadActualStateAsync(Arg.Any<Publication>(), Arg.Any<CancellationToken>())
            .Returns(new PublicationActualState(true, true, true, "8.3.23.1865", null));

        var result = await PublicationsEndpoints.PublishAsync(
            ctx.PublicationId, new PublishPublicationRequest(false),
            ctx.Db, ctx.Webinst, ctx.Job, ctx.Audit, TestHelpers.NewHttpContext("admin"), TimeProvider.System, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<PublicationStatusResponse>>();
        var pub = ctx.Db.Publications.Single();
        pub.Source.Should().Be(PublicationSource.Webinst);
        ctx.Audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.PublicationPublished);
    }

    [Fact]
    public async Task Publish_webinst_failure_returns_409_without_audit()
    {
        await using var ctx = await TestContext.NewAsync(source: PublicationSource.Unknown);
        ctx.Webinst.PublishAsync(Arg.Any<Publication>(), Arg.Any<Infobase>(), Arg.Any<CancellationToken>())
            .Returns(WebinstResult.Failed("Не удалось опубликовать инфобазу через webinst."));

        var result = await PublicationsEndpoints.PublishAsync(
            ctx.PublicationId, new PublishPublicationRequest(false),
            ctx.Db, ctx.Webinst, ctx.Job, ctx.Audit, TestHelpers.NewHttpContext("admin"), TimeProvider.System, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.PublishFailed);
        ctx.Audit.Entries.Should().BeEmpty();
        ctx.Db.Publications.Single().Source.Should().Be(PublicationSource.Unknown);
    }

    [Fact]
    public async Task Publish_gate_requires_confirm_for_non_webinst_published()
    {
        // Чужая (Configurator) и уже опубликованная → без Confirm: 409, webinst не зовём.
        await using var ctx = await TestContext.NewAsync(
            source: PublicationSource.Configurator, status: PublicationPublishStatus.Published);

        var result = await PublicationsEndpoints.PublishAsync(
            ctx.PublicationId, new PublishPublicationRequest(false),
            ctx.Db, ctx.Webinst, ctx.Job, ctx.Audit, TestHelpers.NewHttpContext("admin"), TimeProvider.System, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.PublishConfirmRequired);
        await ctx.Webinst.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!, default);
    }

    [Fact]
    public async Task Publish_gate_passes_with_confirm()
    {
        await using var ctx = await TestContext.NewAsync(
            source: PublicationSource.Configurator, status: PublicationPublishStatus.Published);
        ctx.Webinst.PublishAsync(Arg.Any<Publication>(), Arg.Any<Infobase>(), Arg.Any<CancellationToken>())
            .Returns(WebinstResult.Ok());
        ctx.Iis.ReadActualStateAsync(Arg.Any<Publication>(), Arg.Any<CancellationToken>())
            .Returns(new PublicationActualState(true, true, true, "8.3.23.1865", null));

        var result = await PublicationsEndpoints.PublishAsync(
            ctx.PublicationId, new PublishPublicationRequest(true),
            ctx.Db, ctx.Webinst, ctx.Job, ctx.Audit, TestHelpers.NewHttpContext("admin"), TimeProvider.System, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<PublicationStatusResponse>>();
        ctx.Db.Publications.Single().Source.Should().Be(PublicationSource.Webinst);
    }

    // ── change-platform ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePlatform_success_updates_version_and_writes_213_audit()
    {
        await using var ctx = await TestContext.NewAsync();
        ctx.Platforms.FindPlatformVersions().Returns(new[] { new PlatformVersionInfo("8.3.24.1234", "x64") });
        ctx.Iis.ChangePlatformAsync(Arg.Any<Publication>(), "8.3.24.1234", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        ctx.Iis.ReadActualStateAsync(Arg.Any<Publication>(), Arg.Any<CancellationToken>())
            .Returns(new PublicationActualState(true, true, true, "8.3.24.1234", null));

        var result = await PublicationsEndpoints.ChangePlatformAsync(
            ctx.PublicationId, new ChangePlatformRequest("8.3.24.1234"),
            ctx.Db, ctx.Iis, ctx.Platforms, ctx.Job, ctx.Audit, TestHelpers.NewHttpContext("admin"),
            TimeProvider.System, NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<Ok<PublicationStatusResponse>>();
        ctx.Db.Publications.Single().PlatformVersion.Should().Be("8.3.24.1234");
        ctx.Audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.PublicationPlatformChanged);
    }

    [Fact]
    public async Task ChangePlatform_invalid_version_returns_validation_problem()
    {
        await using var ctx = await TestContext.NewAsync();

        var result = await PublicationsEndpoints.ChangePlatformAsync(
            ctx.PublicationId, new ChangePlatformRequest("not-a-version"),
            ctx.Db, ctx.Iis, ctx.Platforms, ctx.Job, ctx.Audit, TestHelpers.NewHttpContext("admin"),
            TimeProvider.System, NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
        await ctx.Iis.DidNotReceiveWithAnyArgs().ChangePlatformAsync(default!, default!, default);
    }

    [Fact]
    public async Task ChangePlatform_version_not_installed_returns_validation_problem()
    {
        await using var ctx = await TestContext.NewAsync();
        ctx.Platforms.FindPlatformVersions().Returns(new[] { new PlatformVersionInfo("8.3.23.1865", "x64") });

        var result = await PublicationsEndpoints.ChangePlatformAsync(
            ctx.PublicationId, new ChangePlatformRequest("8.3.99.9999"),
            ctx.Db, ctx.Iis, ctx.Platforms, ctx.Job, ctx.Audit, TestHelpers.NewHttpContext("admin"),
            TimeProvider.System, NullLoggerFactory.Instance, CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
    }

    [Fact]
    public async Task ChangePlatform_iis_exception_returns_409_sanitized_without_audit()
    {
        await using var ctx = await TestContext.NewAsync();
        ctx.Platforms.FindPlatformVersions().Returns(new[] { new PlatformVersionInfo("8.3.24.1234", "x64") });
        const string secret = "C:\\inetpub\\wwwroot\\acme\\web.config access denied";
        ctx.Iis.ChangePlatformAsync(Arg.Any<Publication>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException(secret));

        var result = await PublicationsEndpoints.ChangePlatformAsync(
            ctx.PublicationId, new ChangePlatformRequest("8.3.24.1234"),
            ctx.Db, ctx.Iis, ctx.Platforms, ctx.Job, ctx.Audit, TestHelpers.NewHttpContext("admin"),
            TimeProvider.System, NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Detail.Should().NotContain(secret);
        ctx.Audit.Entries.Should().BeEmpty();
        ctx.Db.Publications.Single().PlatformVersion.Should().Be("8.3.23.1865", "версия не меняется при отказе IIS");
    }

    private sealed class TestContext : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid InfobaseId { get; } = Guid.NewGuid();
        public Guid PublicationId { get; } = Guid.NewGuid();

        public TestHelpers.CapturingAuditLogger Audit { get; } = new();
        public Infrastructure.Persistence.AppDbContext Db { get; } = TestHelpers.NewInMemoryDb();
        public IIisPublishingService Iis { get; } = Substitute.For<IIisPublishingService>();
        public IWebinstPublisher Webinst { get; } = Substitute.For<IWebinstPublisher>();
        public IPlatformVersionDiscovery Platforms { get; } = Substitute.For<IPlatformVersionDiscovery>();
        public IPublicationStatusJob Job { get; private set; } = null!;

        public static async Task<TestContext> NewAsync(
            PublicationSource source = PublicationSource.Unknown,
            PublicationPublishStatus status = PublicationPublishStatus.Unknown)
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
                DatabaseServer = "sql.local",
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
                Source = source,
                LastCheckStatus = status,
                CreatedAt = DateTime.UtcNow,
            });
            await ctx.Db.SaveChangesAsync();

            var settings = Substitute.For<ISettingsSnapshot>();
            settings.GetInt(Arg.Any<string>()).Returns((int?)null);

            ctx.Job = new PublicationStatusRefreshJob(
                ctx.Db,
                ctx.Iis,
                settings,
                new StatusRefreshThrottleState(),
                TimeProvider.System,
                NullLogger<PublicationStatusRefreshJob>.Instance);
            return ctx;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
