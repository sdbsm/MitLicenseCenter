using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Tests.Unit.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Performance;

// MLC-070 (ADR-26): persistence-инварианты сущностей записи на реальном провайдере (SQLite-in-memory,
// схема из той же модели). Канон — AppDbContext.OnModelCreating. EF InMemory не соблюдает FK-cascade,
// поэтому каскад удаления сэмплов проверяется тут.
public sealed class PerfRecordingPersistenceTests
{
    private static readonly string[] RecordingSampleIndex = ["RecordingId", "SampleUtc"];
    private static readonly string[] RecordingStartedAtIndex = ["StartedAtUtc"];

    [Fact]
    public void Entities_are_mapped_to_dbo_tables_with_expected_indexes()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var recording = db.Model.FindEntityType(typeof(PerfRecording))!;
        recording.GetTableName().Should().Be("PerfRecordings");
        recording.GetSchema().Should().Be("dbo");
        recording.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(RecordingStartedAtIndex));

        var sample = db.Model.FindEntityType(typeof(PerfRecordingSample))!;
        sample.GetTableName().Should().Be("PerfRecordingSamples");
        sample.GetSchema().Should().Be("dbo");
        sample.GetIndexes().Should().Contain(
            i => i.Properties.Select(p => p.Name).SequenceEqual(RecordingSampleIndex));
    }

    [Fact]
    public void Deleting_a_recording_cascades_to_its_samples()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var recordingId = Guid.NewGuid();
        using (var seed = sqlite.NewContext())
        {
            seed.PerfRecordings.Add(new PerfRecording
            {
                Id = recordingId,
                StartedAtUtc = DateTime.UtcNow,
                Status = PerfRecordingStatus.Stopped,
                StopReason = PerfRecordingStopReason.Manual,
                StartedBy = "admin",
            });
            seed.PerfRecordingSamples.Add(NewSample(recordingId));
            seed.PerfRecordingSamples.Add(NewSample(recordingId));
            seed.SaveChanges();
        }

        // Чистый контекст: каскад выполняет СУБД (сэмплы не отслеживаются change-tracker'ом).
        using (var del = sqlite.NewContext())
        {
            var tracked = del.PerfRecordings.Single(x => x.Id == recordingId);
            del.PerfRecordings.Remove(tracked);
            del.SaveChanges();
        }

        using var verify = sqlite.NewContext();
        verify.PerfRecordings.Count().Should().Be(0);
        verify.PerfRecordingSamples.Count().Should().Be(0, "FK Cascade: удаление записи сносит её сэмплы");
    }

    private static PerfRecordingSample NewSample(Guid recordingId) => new()
    {
        Id = Guid.NewGuid(),
        RecordingId = recordingId,
        SampleUtc = DateTime.UtcNow,
        Measuring = false,
        ProcessGroupsJson = "[]",
    };
}
