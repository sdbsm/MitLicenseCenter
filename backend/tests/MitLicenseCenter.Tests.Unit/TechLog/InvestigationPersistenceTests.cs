using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.TechLog;
using MitLicenseCenter.Tests.Unit.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.TechLog;

// MLC-237 (этап C): persistence-инварианты «Дела» расследования на реальном провайдере (SQLite-in-memory,
// схема из той же модели). Канон — AppDbContext.OnModelCreating. Заменяет TechLogCollectionPersistenceTests
// (сущность мигрирована в Investigation). Проверяет: маппинг таблицы/индексов, round-trip enum'ов (int),
// owned CollectionConfig, Finding round-trip JSON, каскадное удаление Findings.
public sealed class InvestigationPersistenceTests
{
    private static readonly string[] StartedAtIndex = ["StartedAtUtc"];
    private static readonly string[] StatusIndex = ["Status"];
    private static readonly string[] FindingInvestigationIndex = ["InvestigationId"];

    [Fact]
    public void Entity_is_mapped_to_dbo_table_with_expected_indexes()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var entity = db.Model.FindEntityType(typeof(Investigation))!;
        entity.GetTableName().Should().Be("Investigations");
        entity.GetSchema().Should().Be("dbo");
        entity.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(StartedAtIndex));
        entity.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(StatusIndex));

        var finding = db.Model.FindEntityType(typeof(Finding))!;
        finding.GetTableName().Should().Be("Findings");
        finding.GetSchema().Should().Be("dbo");
        finding.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(FindingInvestigationIndex));
    }

    [Fact]
    public void Scenario_status_and_stop_reason_round_trip_as_int()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var id = Guid.NewGuid();

        using (var seed = sqlite.NewContext())
        {
            seed.Investigations.Add(new Investigation
            {
                Id = id,
                Scenario = InvestigationScenario.SlowQueries,
                Status = InvestigationStatus.Completed,
                StopReason = InvestigationStopReason.DiskLimit,
                StartedAtUtc = DateTime.UtcNow,
                StoppedAtUtc = DateTime.UtcNow,
                StartedBy = "admin",
                InfobaseProcessName = "mitpro",
                CollectionDirectory = @"C:\techlog",
                ConfigMarker = "managed by MitLicenseCenter",
            });
            seed.SaveChanges();
        }

        using var verify = sqlite.NewContext();
        var row = verify.Investigations.Single(x => x.Id == id);
        row.Scenario.Should().Be(InvestigationScenario.SlowQueries);
        row.Status.Should().Be(InvestigationStatus.Completed);
        row.StopReason.Should().Be(InvestigationStopReason.DiskLimit);
        row.StartedBy.Should().Be("admin");
    }

    [Fact]
    public void Owned_collection_config_round_trips()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var id = Guid.NewGuid();
        var infobaseId = Guid.NewGuid();

        using (var seed = sqlite.NewContext())
        {
            seed.Investigations.Add(new Investigation
            {
                Id = id,
                Scenario = InvestigationScenario.Locks,
                Status = InvestigationStatus.Collecting,
                StartedAtUtc = DateTime.UtcNow,
                StartedBy = "admin",
                InfobaseId = infobaseId,
                InfobaseProcessName = "mitpro",
                CollectionDirectory = @"C:\techlog",
                ConfigMarker = "managed by MitLicenseCenter",
                CollectionConfig = new CollectionConfig
                {
                    LogcfgLocation = @"C:\techlog",
                    Events = "TLOCK,TTIMEOUT,TDEADLOCK",
                    DurationThresholdMicros = 3_000_000,
                    ProcessNameFilter = "mitpro",
                    Format = "json",
                    HistoryHours = 2,
                },
            });
            seed.SaveChanges();
        }

        using var verify = sqlite.NewContext();
        var row = verify.Investigations.Single(x => x.Id == id);
        row.CollectionConfig.Should().NotBeNull();
        row.CollectionConfig!.LogcfgLocation.Should().Be(@"C:\techlog");
        row.CollectionConfig.Events.Should().Be("TLOCK,TTIMEOUT,TDEADLOCK");
        row.CollectionConfig.DurationThresholdMicros.Should().Be(3_000_000);
        row.CollectionConfig.ProcessNameFilter.Should().Be("mitpro");
        row.CollectionConfig.Format.Should().Be("json");
        row.CollectionConfig.HistoryHours.Should().Be(2);
    }

    [Fact]
    public void Investigation_without_config_persists_as_null_owned()
    {
        // Механический перенос исторических TechLogCollections не наполняет снимок → owned-ссылка null.
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var id = Guid.NewGuid();

        using (var seed = sqlite.NewContext())
        {
            seed.Investigations.Add(new Investigation
            {
                Id = id,
                Scenario = InvestigationScenario.Locks,
                Status = InvestigationStatus.Interrupted,
                StartedAtUtc = DateTime.UtcNow,
                StartedBy = "system",
                CollectionDirectory = @"C:\techlog",
                ConfigMarker = "managed by MitLicenseCenter",
            });
            seed.SaveChanges();
        }

        using var verify = sqlite.NewContext();
        var row = verify.Investigations.Single(x => x.Id == id);
        row.CollectionConfig.Should().BeNull();
    }

    [Fact]
    public void Finding_round_trips_versioned_json()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var investigationId = Guid.NewGuid();
        var findingId = Guid.NewGuid();
        const string json = """{"top":[{"sql":"SELECT 1","durationMicros":4200000}]}""";

        using (var seed = sqlite.NewContext())
        {
            seed.Investigations.Add(NewInvestigation(investigationId));
            seed.Findings.Add(new Finding
            {
                Id = findingId,
                InvestigationId = investigationId,
                Kind = FindingKind.SlowQueries,
                SchemaVersion = 1,
                ResultJson = json,
            });
            seed.SaveChanges();
        }

        using var verify = sqlite.NewContext();
        var finding = verify.Findings.Single(x => x.Id == findingId);
        finding.Kind.Should().Be(FindingKind.SlowQueries);
        finding.SchemaVersion.Should().Be(1);
        finding.ResultJson.Should().Be(json);
    }

    [Fact]
    public void Deleting_an_investigation_cascades_to_its_findings()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var investigationId = Guid.NewGuid();

        using (var seed = sqlite.NewContext())
        {
            seed.Investigations.Add(NewInvestigation(investigationId));
            seed.Findings.Add(NewFinding(investigationId));
            seed.Findings.Add(NewFinding(investigationId));
            seed.SaveChanges();
        }

        // Чистый контекст: каскад выполняет СУБД (Findings не отслеживаются change-tracker'ом).
        using (var del = sqlite.NewContext())
        {
            var tracked = del.Investigations.Single(x => x.Id == investigationId);
            del.Investigations.Remove(tracked);
            del.SaveChanges();
        }

        using var verify = sqlite.NewContext();
        verify.Investigations.Count().Should().Be(0);
        verify.Findings.Count().Should().Be(0, "FK Cascade: удаление дела сносит его Findings");
    }

    private static Investigation NewInvestigation(Guid id) => new()
    {
        Id = id,
        Scenario = InvestigationScenario.SlowQueries,
        Status = InvestigationStatus.Completed,
        StopReason = InvestigationStopReason.Manual,
        StartedAtUtc = DateTime.UtcNow,
        StoppedAtUtc = DateTime.UtcNow,
        StartedBy = "admin",
        CollectionDirectory = @"C:\techlog",
        ConfigMarker = "managed by MitLicenseCenter",
    };

    private static Finding NewFinding(Guid investigationId) => new()
    {
        Id = Guid.NewGuid(),
        InvestigationId = investigationId,
        Kind = FindingKind.ManagedLocks,
        SchemaVersion = 1,
        ResultJson = "{}",
    };
}
