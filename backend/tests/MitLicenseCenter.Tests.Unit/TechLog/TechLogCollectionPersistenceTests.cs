using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.TechLog;
using MitLicenseCenter.Tests.Unit.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-230: persistence-инварианты дела сбора ТЖ на реальном провайдере (SQLite-in-memory, схема из
// той же модели). Канон — AppDbContext.OnModelCreating. Enum'ы Status/StopReason — int (HasConversion).
public sealed class TechLogCollectionPersistenceTests
{
    private static readonly string[] StartedAtIndex = ["StartedAtUtc"];
    private static readonly string[] StatusIndex = ["Status"];

    [Fact]
    public void Entity_is_mapped_to_dbo_table_with_expected_indexes()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var entity = db.Model.FindEntityType(typeof(TechLogCollection))!;
        entity.GetTableName().Should().Be("TechLogCollections");
        entity.GetSchema().Should().Be("dbo");
        entity.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(StartedAtIndex));
        entity.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(StatusIndex));
    }

    [Fact]
    public void Status_and_stop_reason_round_trip_as_int()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var id = Guid.NewGuid();

        using (var seed = sqlite.NewContext())
        {
            seed.TechLogCollections.Add(new TechLogCollection
            {
                Id = id,
                Status = TechLogCollectionStatus.Stopped,
                StopReason = TechLogCollectionStopReason.DiskLimit,
                StartedAtUtc = DateTime.UtcNow,
                StoppedAtUtc = DateTime.UtcNow,
                Scenario = "SlowQueries",
                InfobaseProcessName = "mitpro",
                CollectionDirectory = @"C:\techlog",
                ConfigMarker = "managed by MitLicenseCenter",
            });
            seed.SaveChanges();
        }

        using var verify = sqlite.NewContext();
        var row = verify.TechLogCollections.Single(x => x.Id == id);
        row.Status.Should().Be(TechLogCollectionStatus.Stopped);
        row.StopReason.Should().Be(TechLogCollectionStopReason.DiskLimit);
        row.Scenario.Should().Be("SlowQueries");
    }
}
