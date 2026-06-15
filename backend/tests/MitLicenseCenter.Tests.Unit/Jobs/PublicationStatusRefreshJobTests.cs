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

    // MLC-163 — регрессия: ручная «Проверить» (RefreshOneAsync) бросала
    // DbUpdateConcurrencyException ВСЕГДА. Первопричина: ProcessOneAsync грузил снимок
    // публикации проекцией БЕЗ RowVersion и аттачил probe с пустым concurrency-токеном
    // (MLC-151) → EF строил targeted-UPDATE `… WHERE Id=@id AND RowVersion=@token` с
    // пустым токеном → 0 строк → исключение → HTTP 500.
    //
    // Тест на РЕАЛЬНОМ провайдере (SQLite): IsRowVersion() помечает свойство
    // IsConcurrencyToken, поэтому WHERE RowVersion реально проверяется СУБД (EF InMemory
    // конкурентность игнорирует — на нём баг невоспроизводим). Строке проставляется
    // НЕПУСТОЙ токен сырым UPDATE (SQLite не генерирует rowversion сам); фикс проецирует
    // p.RowVersion в снимок и подставляет probe.RowVersion = snapshot.RowVersion →
    // токен совпадает → UPDATE затрагивает 1 строку → LastCheck* записываются.
    [Fact]
    public async Task RefreshOne_with_nonempty_rowversion_writes_status_without_concurrency_exception()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var tenantId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();

        await using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Acme",
                MaxConcurrentLicenses = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            seed.Infobases.Add(NewInfobase(infobaseId, tenantId, "Acme A", "acme_a"));
            seed.Publications.Add(NewPublication(publicationId, infobaseId, "/works"));
            await seed.SaveChangesAsync();

            // SQLite не генерирует rowversion автоматически — проставляем НЕПУСТОЙ токен
            // сырым SQL, чтобы воспроизвести прод-инвариант (после MLC-151 у строки в
            // SQL Server токен непустой). Так WHERE RowVersion реально дискриминирует баг.
            await seed.Database.ExecuteSqlRawAsync(
                "UPDATE Publications SET RowVersion = X'0102030405060708' WHERE Id = {0}",
                publicationId);
        }

        var iis = Substitute.For<IIisPublishingService>();
        iis.ReadActualStateAsync(Arg.Any<Publication>(), Arg.Any<CancellationToken>())
            .Returns(new PublicationActualState(true, true, true, "8.3.23.1865", null));

        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(Arg.Any<string>()).Returns((int?)null);

        await using (var db = sqlite.NewContext())
        {
            var job = new PublicationStatusRefreshJob(
                db, iis, settings, new StatusRefreshThrottleState(),
                TimeProvider.System, NullLogger<PublicationStatusRefreshJob>.Instance);

            // До фикса бросало DbUpdateConcurrencyException (0 строк). После — проходит.
            await job.RefreshOneAsync(publicationId, CancellationToken.None);
        }

        await using (var assert = sqlite.NewContext())
        {
            var saved = assert.Publications.AsNoTracking().Single(p => p.Id == publicationId);
            saved.LastCheckStatus.Should().Be(PublicationPublishStatus.Published);
            saved.LastCheckAt.Should().NotBeNull();
        }
    }

    // MLC-163 — benign-конфликт: реальный редкий DbUpdateConcurrencyException (строку
    // изменили параллельно между загрузкой снимка и сохранением) НЕ должен отдавать 500.
    // RefreshOneAsync ловит его как benign (лог + выход); статус подтянется следующим
    // циклом. Конфликт эмулируем перехватчиком (как InfobaseConcurrencyTests/MLC-151),
    // т.к. детерминированно воспроизвести гонку в тесте нельзя.
    [Fact]
    public async Task RefreshOne_swallows_real_concurrency_conflict_instead_of_throwing()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            new DbUpdateConcurrencyException(
                "Database operation expected to affect 1 row(s) but actually affected 0 row(s)."));
        await using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);

        var tenantId = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Infobases.Add(NewInfobase(infobaseId, tenantId, "Acme A", "acme_a"));
        db.Publications.Add(NewPublication(publicationId, infobaseId, "/works"));
        await db.SaveChangesAsync();

        var iis = Substitute.For<IIisPublishingService>();
        iis.ReadActualStateAsync(Arg.Any<Publication>(), Arg.Any<CancellationToken>())
            .Returns(new PublicationActualState(true, true, true, "8.3.23.1865", null));

        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(Arg.Any<string>()).Returns((int?)null);

        var job = new PublicationStatusRefreshJob(
            db, iis, settings, new StatusRefreshThrottleState(),
            TimeProvider.System, NullLogger<PublicationStatusRefreshJob>.Instance);

        interceptor.Armed = true;

        // Не должно бросать наружу — иначе ручная «Проверить» вернула бы 500.
        var act = async () => await job.RefreshOneAsync(publicationId, CancellationToken.None);
        await act.Should().NotThrowAsync();
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
