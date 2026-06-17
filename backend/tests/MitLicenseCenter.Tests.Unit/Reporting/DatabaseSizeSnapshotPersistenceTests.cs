using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Tests.Unit.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Reporting;

// MLC-185: persistence-инварианты сущности-телеметрии размера баз на реальном
// провайдере (SQLite-in-memory, схема из той же модели). Канон — AppDbContext.OnModelCreating.
public sealed class DatabaseSizeSnapshotPersistenceTests
{
    private static readonly string[] DatabaseSnapshotIndex = ["DatabaseName", "SnapshotAtUtc"];
    private static readonly string[] TenantSnapshotIndex = ["TenantId", "SnapshotAtUtc"];

    [Fact]
    public void Entity_is_mapped_to_dbo_table_with_reporting_indexes()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var et = db.Model.FindEntityType(typeof(DatabaseSizeSnapshot))!;
        et.GetTableName().Should().Be("DatabaseSizeSnapshots");
        et.GetSchema().Should().Be("dbo");

        et.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(DatabaseSnapshotIndex),
            "дорога чтения отчётов: ряд по базе во времени (DatabaseName + SnapshotAtUtc)");
        et.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(TenantSnapshotIndex),
            "дорога чтения отчётов: срез по клиенту во времени (TenantId + SnapshotAtUtc)");
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
        var snapshot = new DatabaseSizeSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            DatabaseName = "acme_accounting",
            SnapshotAtUtc = DateTime.UtcNow,
            DataBytes = 1_073_741_824,
            LogBytes = 268_435_456,
        };
        using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(tenant);
            seed.DatabaseSizeSnapshots.Add(snapshot);
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
        var row = verify.DatabaseSizeSnapshots.Single(x => x.Id == snapshot.Id);
        row.TenantId.Should().BeNull(
            "FK SetNull: история размера базы переживает удаление клиента");
        row.DatabaseName.Should().Be("acme_accounting",
            "имя базы — собственный ключ сопоставления, не зависит от привязки к клиенту");
    }
}
