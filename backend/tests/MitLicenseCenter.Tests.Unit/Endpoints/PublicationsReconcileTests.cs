using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
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

// Покрывает три ключевых пути reconcile-endpoint'а:
//   • happy path → 200 + drift-status + audit 211 (PublicationReconciled).
//   • IIS exception → 409 ProblemDetails с code IIS_RECONCILE_FAILED, audit НЕ пишется.
//   • publication not found → 404.
public sealed class PublicationsReconcileTests
{
    [Fact]
    public async Task Reconcile_success_returns_200_and_writes_211_audit()
    {
        await using var ctx = await TestContext.NewAsync(initialStatus: PublicationDriftStatus.Drift);
        // ApplyDesiredStateAsync — successful no-op. Drift detector затем найдёт InSync.
        ctx.Iis.ApplyDesiredStateAsync(Arg.Any<Publication>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        ctx.Iis.ReadActualStateAsync(Arg.Any<Publication>(), Arg.Any<CancellationToken>())
            .Returns(new PublicationActualState(
                SiteExists: true,
                VirtualPathExists: true,
                PlatformVersion: "8.3.23.1865",
                EnableOData: true,
                EnableHttpServices: true,
                VrdContent: "<point/>",
                Error: null));

        var result = await PublicationsEndpoints.ReconcileAsync(
            ctx.PublicationId,
            ctx.Db,
            ctx.Iis,
            ctx.Job,
            ctx.Audit,
            TestHelpers.NewHttpContext("admin"),
            CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<DriftStatusResponse>>().Subject;
        ok.Value!.Status.Should().Be(PublicationDriftStatus.InSync);

        ctx.Audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.PublicationReconciled);
        ctx.Audit.Entries.Should().NotContain(e => e.Action == AuditActionType.PublicationDriftDetected,
            "transition Drift→InSync не должен ронять 210 — это работа reconcile, не drift-job'а");
    }

    [Fact]
    public async Task Reconcile_iis_exception_returns_409_without_audit()
    {
        await using var ctx = await TestContext.NewAsync(initialStatus: PublicationDriftStatus.Drift);
        // IOException — один из перехватываемых reconcile-endpoint'ом типов (тот
        // же путь обработки, что у COMException; CA2201 не разрешает кидать
        // COMException в тестах).
        ctx.Iis.ApplyDesiredStateAsync(Arg.Any<Publication>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("Access denied to IIS metabase"));

        var result = await PublicationsEndpoints.ReconcileAsync(
            ctx.PublicationId,
            ctx.Db,
            ctx.Iis,
            ctx.Job,
            ctx.Audit,
            TestHelpers.NewHttpContext("admin"),
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.IisReconcileFailed);
        ctx.Audit.Entries.Should().BeEmpty("аудит не пишется при отказе IIS");
    }

    [Fact]
    public async Task Reconcile_publication_not_found_returns_404()
    {
        await using var ctx = await TestContext.NewAsync(initialStatus: PublicationDriftStatus.InSync);

        var result = await PublicationsEndpoints.ReconcileAsync(
            Guid.NewGuid(), // несуществующая публикация
            ctx.Db,
            ctx.Iis,
            ctx.Job,
            ctx.Audit,
            TestHelpers.NewHttpContext("admin"),
            CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
        ctx.Audit.Entries.Should().BeEmpty();
        await ctx.Iis.DidNotReceiveWithAnyArgs().ApplyDesiredStateAsync(default!, default);
    }

    private sealed class TestContext : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid InfobaseId { get; } = Guid.NewGuid();
        public Guid PublicationId { get; } = Guid.NewGuid();

        public TestHelpers.CapturingAuditLogger Audit { get; } = new();
        public Infrastructure.Persistence.AppDbContext Db { get; } = TestHelpers.NewInMemoryDb();
        public IIisPublishingService Iis { get; } = Substitute.For<IIisPublishingService>();
        public IDriftCheckJob Job { get; private set; } = null!;

        public static async Task<TestContext> NewAsync(PublicationDriftStatus initialStatus)
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
                EnableOData = true,
                EnableHttpServices = true,
                CreatedAt = DateTime.UtcNow,
                LastDriftStatus = initialStatus,
                LastDriftCheckAt = DateTime.UtcNow.AddMinutes(-5),
            });
            await ctx.Db.SaveChangesAsync();

            var settings = Substitute.For<ISettingsSnapshot>();
            settings.GetInt(Arg.Any<string>()).Returns((int?)null);

            ctx.Job = new DriftCheckJob(
                ctx.Db,
                ctx.Iis,
                ctx.Audit,
                settings,
                new DriftThrottleState(),
                TimeProvider.System,
                NullLogger<DriftCheckJob>.Instance);
            return ctx;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
