using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Infrastructure.Persistence;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// Schema-level гарантии PR 2.3: unique (TenantId, Name) для Infobase,
// FK Publication→Infobase = Cascade, FK Infobase→Tenant = Restrict.
// InMemory-провайдер не уважает FK/unique в runtime, но метаданные модели
// неизменны — этого достаточно, чтобы рефакторинг AppDbContext не сломал контракт.
public sealed class InfobaseSchemaTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"schema-{Guid.NewGuid():N}")
            .Options);

    [Fact]
    public void Infobases_have_unique_TenantId_Name_index()
    {
        using var db = NewDb();
        var entity = db.Model.FindEntityType(typeof(Infobase))!;

        var compositeUnique = entity.GetIndexes().SingleOrDefault(i =>
            i.IsUnique
            && i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Infobase.TenantId), nameof(Infobase.Name) }));

        compositeUnique.Should().NotBeNull("инфобаза уникальна в пределах клиента, не глобально");
    }

    [Fact]
    public void Infobase_to_Tenant_FK_uses_Restrict()
    {
        using var db = NewDb();
        var entity = db.Model.FindEntityType(typeof(Infobase))!;
        var fk = entity.GetForeignKeys().Single();

        fk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void Publication_to_Infobase_FK_uses_Cascade_and_is_unique()
    {
        using var db = NewDb();
        var entity = db.Model.FindEntityType(typeof(Publication))!;
        var fk = entity.GetForeignKeys().Single();

        fk.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        fk.IsUnique.Should().BeTrue("Publication 1-to-1 required, не many-to-one");
    }
}
