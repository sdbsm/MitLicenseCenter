using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// BE-05 (MLC-114) — изоляция сбоя одной публикации в RefreshAllAsync. Раньше один
// упавший элемент (напр. DbUpdateConcurrencyException при параллельном удалении) ронял
// весь цикл, но джоба «завершалась успешно» — статус остальных не обновлялся.
public sealed class PublicationStatusRefreshJobTests
{
    [Fact]
    public async Task RefreshAll_one_item_throws_still_processes_the_rest()
    {
        await using var db = TestHelpers.NewInMemoryDb();

        var tenantId = Guid.NewGuid();
        // Publication ↔ Infobase — 1:1 (FK InfobaseId уникален), поэтому под каждую
        // публикацию заводим отдельную инфобазу.
        var infobaseFailing = Guid.NewGuid();
        var infobaseOk = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Infobases.Add(NewInfobase(infobaseFailing, tenantId, "Acme A", "acme_a"));
        db.Infobases.Add(NewInfobase(infobaseOk, tenantId, "Acme B", "acme_b"));

        var failingId = Guid.NewGuid();
        var okId = Guid.NewGuid();
        db.Publications.Add(NewPublication(failingId, infobaseFailing, "/fails"));
        db.Publications.Add(NewPublication(okId, infobaseOk, "/works"));
        await db.SaveChangesAsync();

        var iis = Substitute.For<IIisPublishingService>();
        // Первый элемент бросает (имитируем сбой чтения IIS), второй читается успешно.
        iis.ReadActualStateAsync(Arg.Is<Publication>(p => p.Id == failingId), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));
        iis.ReadActualStateAsync(Arg.Is<Publication>(p => p.Id == okId), Arg.Any<CancellationToken>())
            .Returns(new PublicationActualState(true, true, true, "8.3.23.1865", null));

        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(Arg.Any<string>()).Returns((int?)null);

        var job = new PublicationStatusRefreshJob(
            db, iis, settings, new StatusRefreshThrottleState(),
            TimeProvider.System, NullLogger<PublicationStatusRefreshJob>.Instance);

        // Не должно бросать наружу — сбой одного элемента логируется и цикл продолжается.
        await job.RefreshAllAsync(CancellationToken.None);

        // Второй (исправный) элемент обновлён, несмотря на сбой первого.
        var ok = db.Publications.AsNoTracking().Single(p => p.Id == okId);
        ok.LastCheckStatus.Should().Be(PublicationPublishStatus.Published);
        ok.LastCheckAt.Should().NotBeNull();

        // Упавший — статус не присвоен (остался Unknown), но он не сорвал обработку второго.
        var failed = db.Publications.AsNoTracking().Single(p => p.Id == failingId);
        failed.LastCheckStatus.Should().Be(PublicationPublishStatus.Unknown);
    }

    private static Infobase NewInfobase(Guid id, Guid tenantId, string name, string dbName) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = name,
        ClusterInfobaseId = Guid.NewGuid(),
        DatabaseName = dbName,
        Status = InfobaseStatus.Active,
        CreatedAt = DateTime.UtcNow,
    };

    private static Publication NewPublication(Guid id, Guid infobaseId, string virtualPath) => new()
    {
        Id = id,
        InfobaseId = infobaseId,
        SiteName = "Default Web Site",
        VirtualPath = virtualPath,
        PlatformVersion = "8.3.23.1865",
        Source = PublicationSource.Webinst,
        LastCheckStatus = PublicationPublishStatus.Unknown,
        CreatedAt = DateTime.UtcNow,
    };
}
