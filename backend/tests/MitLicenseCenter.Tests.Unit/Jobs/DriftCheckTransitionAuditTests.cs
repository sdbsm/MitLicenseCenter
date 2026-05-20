using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// Контракт PR 3.5: drift-job пишет AuditActionType.PublicationDriftDetected=210
// ТОЛЬКО на transition AND ТОЛЬКО когда новый статус ∈ {Drift, Missing, Error}.
// Reconcile-успех (transition Drift→InSync) — 211 пишет endpoint, а НЕ job.
public sealed class DriftCheckTransitionAuditTests
{
    [Fact]
    public async Task InSync_to_InSync_writes_no_audit()
    {
        await using var ctx = await NewContextAsync(initialStatus: PublicationDriftStatus.InSync);

        ctx.SetActualState(MakeActual(odata: true, http: true, version: "8.3.23.1865"));

        await ctx.Job.CheckOneAsync(ctx.PublicationId, CancellationToken.None);

        ctx.Audit.Entries.Should().BeEmpty();
        (await ctx.GetStatusAsync()).Should().Be(PublicationDriftStatus.InSync);
    }

    [Fact]
    public async Task InSync_to_Drift_writes_one_210_row()
    {
        await using var ctx = await NewContextAsync(initialStatus: PublicationDriftStatus.InSync);

        ctx.SetActualState(MakeActual(odata: false, http: true, version: "8.3.23.1865"));

        await ctx.Job.CheckOneAsync(ctx.PublicationId, CancellationToken.None);

        ctx.Audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.PublicationDriftDetected);
        (await ctx.GetStatusAsync()).Should().Be(PublicationDriftStatus.Drift);
    }

    [Fact]
    public async Task Drift_to_Drift_same_details_writes_no_audit()
    {
        await using var ctx = await NewContextAsync(initialStatus: PublicationDriftStatus.InSync);

        // First transition: InSync → Drift (1 audit row).
        ctx.SetActualState(MakeActual(odata: false, http: true, version: "8.3.23.1865"));
        await ctx.Job.CheckOneAsync(ctx.PublicationId, CancellationToken.None);

        // Second call: тот же дрейф → 0 новых audit-строк.
        await ctx.Job.CheckOneAsync(ctx.PublicationId, CancellationToken.None);

        ctx.Audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.PublicationDriftDetected);
    }

    [Fact]
    public async Task Drift_to_InSync_writes_no_audit_from_drift_job()
    {
        // Этот тест фиксирует, что drift-job сам по себе НЕ пишет 211 — это
        // забота reconcile-endpoint'а. PublicationsReconcileTests покрывает обратное.
        await using var ctx = await NewContextAsync(initialStatus: PublicationDriftStatus.Drift);

        // ActualState уже соответствует desired → CheckOneAsync найдёт InSync.
        ctx.SetActualState(MakeActual(odata: true, http: true, version: "8.3.23.1865"));

        await ctx.Job.CheckOneAsync(ctx.PublicationId, CancellationToken.None);

        ctx.Audit.Entries.Should().BeEmpty();
        (await ctx.GetStatusAsync()).Should().Be(PublicationDriftStatus.InSync);
    }

    [Fact]
    public async Task InSync_to_Missing_writes_audit_with_tenantId()
    {
        await using var ctx = await NewContextAsync(initialStatus: PublicationDriftStatus.InSync);

        ctx.SetActualState(new PublicationActualState(
            SiteExists: false,
            VirtualPathExists: false,
            PlatformVersion: null,
            EnableOData: false,
            EnableHttpServices: false,
            VrdContent: null,
            Error: null));

        await ctx.Job.CheckOneAsync(ctx.PublicationId, CancellationToken.None);

        ctx.Audit.Entries.Should().ContainSingle(e =>
            e.Action == AuditActionType.PublicationDriftDetected
            && e.TenantId == ctx.TenantId);
    }

    private static PublicationActualState MakeActual(bool odata, bool http, string version) =>
        new(
            SiteExists: true,
            VirtualPathExists: true,
            PlatformVersion: version,
            EnableOData: odata,
            EnableHttpServices: http,
            VrdContent: "<point/>",
            Error: null);

    private static async Task<TestContext> NewContextAsync(PublicationDriftStatus initialStatus)
    {
        var ctx = new TestContext();
        await ctx.SeedAsync(initialStatus);
        return ctx;
    }

    private sealed class TestContext : IAsyncDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid InfobaseId { get; } = Guid.NewGuid();
        public Guid PublicationId { get; } = Guid.NewGuid();

        public TestHelpers.CapturingAuditLogger Audit { get; } = new();
        public AppDbContext Db { get; } = TestHelpers.NewInMemoryDb();
        public IIisPublishingService Iis { get; } = Substitute.For<IIisPublishingService>();
        public DriftCheckJob Job { get; private set; } = null!;

        public void SetActualState(PublicationActualState state) =>
            Iis.ReadActualStateAsync(Arg.Any<Publication>(), Arg.Any<CancellationToken>()).Returns(state);

        public async Task SeedAsync(PublicationDriftStatus initialStatus)
        {
            Db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Name = "Acme",
                MaxConcurrentLicenses = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            Db.Infobases.Add(new Infobase
            {
                Id = InfobaseId,
                TenantId = TenantId,
                Name = "Acme BP",
                ClusterInfobaseId = Guid.NewGuid(),
                DatabaseServer = "sql.local",
                DatabaseName = "acme_bp",
                Status = InfobaseStatus.Active,
                CreatedAt = DateTime.UtcNow,
            });
            Db.Publications.Add(new Publication
            {
                Id = PublicationId,
                InfobaseId = InfobaseId,
                SiteName = "Default Web Site",
                VirtualPath = "/MyPub",
                PlatformVersion = "8.3.23.1865",
                EnableOData = true,
                EnableHttpServices = true,
                CreatedAt = DateTime.UtcNow,
                LastDriftStatus = initialStatus,
                LastDriftCheckAt = initialStatus == PublicationDriftStatus.InSync ? null : DateTime.UtcNow.AddMinutes(-5),
                LastDriftDetails = initialStatus == PublicationDriftStatus.Drift
                    ? "OData выключен в desired, но включён в VRD."
                    : null,
            });
            await Db.SaveChangesAsync();

            var settings = Substitute.For<ISettingsSnapshot>();
            settings.GetInt(Arg.Any<string>()).Returns((int?)null);

            Job = new DriftCheckJob(
                Db,
                Iis,
                Audit,
                settings,
                new DriftThrottleState(),
                TimeProvider.System,
                NullLogger<DriftCheckJob>.Instance);
        }

        public async Task<PublicationDriftStatus> GetStatusAsync()
        {
            var p = await Db.Publications.AsNoTracking()
                .FirstAsync(x => x.Id == PublicationId);
            return p.LastDriftStatus;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
