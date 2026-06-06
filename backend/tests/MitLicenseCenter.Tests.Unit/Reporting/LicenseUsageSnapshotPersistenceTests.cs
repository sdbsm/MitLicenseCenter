using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Tests.Unit.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Reporting;

// MLC-048 (ADR-25): persistence-инварианты сущности-телеметрии на реальном провайдере
// (SQLite-in-memory, схема из той же модели). Канон — AppDbContext.OnModelCreating.
public sealed class LicenseUsageSnapshotPersistenceTests
{
    private static readonly string[] TenantBucketIndex = ["TenantId", "BucketStartUtc"];

    [Fact]
    public void Entity_is_mapped_to_dbo_table_with_tenant_bucket_index()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var et = db.Model.FindEntityType(typeof(LicenseUsageSnapshot))!;
        et.GetTableName().Should().Be("LicenseUsageSnapshots");
        et.GetSchema().Should().Be("dbo");

        et.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(TenantBucketIndex),
            "дорога чтения отчётов (MLC-049): фильтр по TenantId + диапазон BucketStartUtc");
    }

    [Fact]
    public void Deleting_a_tenant_nulls_the_snapshot_reference_but_keeps_the_row()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        var snapshot = new LicenseUsageSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            BucketStartUtc = DateTime.UtcNow,
            ConsumedMin = 1,
            ConsumedMax = 5,
            ConsumedAvg = 3.0,
            Limit = 10,
        };
        using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(tenant);
            seed.LicenseUsageSnapshots.Add(snapshot);
            seed.SaveChanges();
        }

        // Чистый контекст: SetNull выполняет СУБД (строка телеметрии не отслеживается).
        using (var del = sqlite.NewContext())
        {
            var tracked = del.Tenants.Single(x => x.Id == tenant.Id);
            del.Tenants.Remove(tracked);
            del.SaveChanges();
        }

        using var verify = sqlite.NewContext();
        verify.Tenants.Count().Should().Be(0);
        var row = verify.LicenseUsageSnapshots.Single(x => x.Id == snapshot.Id);
        row.TenantId.Should().BeNull(
            "FK SetNull: история использования переживает удаление клиента");
    }
}
