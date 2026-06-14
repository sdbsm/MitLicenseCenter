using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-070 (ADR-26, Фаза 4): эндпоинты записи. Стиль проекта — invoke internal static handler напрямую.
// Старт/стоп идут через IPerfRecordingService (substitute — проводка исхода); список/просмотр/удаление
// читают AppDbContext напрямую (vertical slice ADR-20). Поведение самого сервиса — PerfRecordingServiceTests.
public sealed class PerfRecordingEndpointsTests
{
    [Fact]
    public async Task StartRecording_returns_created_summary()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        var svc = Substitute.For<IPerfRecordingService>();
        svc.StartAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PerfRecordingStartResult(PerfRecordingStartOutcome.Started, id));
        // Сервис персистит в своём scope; in-memory store общий по имени БД — эмулируем строкой.
        db.PerfRecordings.Add(new PerfRecording
        {
            Id = id,
            StartedAtUtc = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc),
            Status = PerfRecordingStatus.Active,
            StartedBy = "admin",
        });
        await db.SaveChangesAsync();

        var result = await PerformanceEndpoints.StartRecordingAsync(svc, db, TestHelpers.NewHttpContext("admin"), CancellationToken.None);

        var created = result.Result.Should().BeOfType<Created<RecordingSummary>>().Subject;
        created.Value!.Id.Should().Be(id);
        created.Value!.Status.Should().Be(PerfRecordingStatus.Active);
        created.Location.Should().Be($"/api/v1/performance/recordings/{id}");
    }

    [Fact]
    public async Task StartRecording_when_already_active_returns_conflict()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var svc = Substitute.For<IPerfRecordingService>();
        svc.StartAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PerfRecordingStartResult(PerfRecordingStartOutcome.AlreadyActive, Guid.NewGuid()));

        var result = await PerformanceEndpoints.StartRecordingAsync(svc, db, TestHelpers.NewHttpContext(), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.RecordingActive);
    }

    [Fact]
    public async Task StopRecording_returns_ok_summary()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        var svc = Substitute.For<IPerfRecordingService>();
        svc.StopAsync(id, Arg.Any<CancellationToken>()).Returns(PerfRecordingStopOutcome.Stopped);
        db.PerfRecordings.Add(new PerfRecording
        {
            Id = id,
            StartedAtUtc = DateTime.UtcNow,
            StoppedAtUtc = DateTime.UtcNow,
            Status = PerfRecordingStatus.Stopped,
            StopReason = PerfRecordingStopReason.Manual,
            StartedBy = "admin",
        });
        await db.SaveChangesAsync();

        var result = await PerformanceEndpoints.StopRecordingAsync(id, svc, db, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<RecordingSummary>>().Subject;
        ok.Value!.Status.Should().Be(PerfRecordingStatus.Stopped);
        ok.Value!.StopReason.Should().Be(PerfRecordingStopReason.Manual);
    }

    [Fact]
    public async Task StopRecording_when_not_active_returns_not_found()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var svc = Substitute.For<IPerfRecordingService>();
        svc.StopAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(PerfRecordingStopOutcome.NotActive);

        var result = await PerformanceEndpoints.StopRecordingAsync(Guid.NewGuid(), svc, db, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task ListRecordings_returns_summaries_newest_first_with_sample_counts()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        db.PerfRecordings.Add(new PerfRecording
        {
            Id = older,
            StartedAtUtc = new DateTime(2026, 6, 9, 9, 0, 0, DateTimeKind.Utc),
            Status = PerfRecordingStatus.Stopped,
            StopReason = PerfRecordingStopReason.TimeLimit,
            StartedBy = "admin",
        });
        db.PerfRecordings.Add(new PerfRecording
        {
            Id = newer,
            StartedAtUtc = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc),
            Status = PerfRecordingStatus.Active,
            StartedBy = "admin",
        });
        db.PerfRecordingSamples.Add(NewSample(newer));
        db.PerfRecordingSamples.Add(NewSample(newer));
        await db.SaveChangesAsync();

        var result = await PerformanceEndpoints.ListRecordingsAsync(
            page: null, pageSize: null, db, CancellationToken.None);

        var items = result.Value!.Items;
        items.Should().HaveCount(2);
        items[0].Id.Should().Be(newer, "свежие сверху");
        items[0].SampleCount.Should().Be(2);
        items[1].Id.Should().Be(older);
        items[1].SampleCount.Should().Be(0);
    }

    [Fact]
    public async Task GetRecording_returns_detail_with_deserialized_sample_payload()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        db.PerfRecordings.Add(new PerfRecording
        {
            Id = id,
            StartedAtUtc = DateTime.UtcNow,
            Status = PerfRecordingStatus.Active,
            StartedBy = "admin",
        });

        var onec = new OneCLoadSnapshot(
            DateTime.UtcNow,
            [new OneCSessionLoad(Guid.NewGuid(), 1, Guid.NewGuid(), "1CV8C", "Андрей", "PC",
                null, null, 109, 422, 0, -1138560, 0, 0, null)],
            []);
        var sql = new SqlPerformanceSnapshot(
            DateTime.UtcNow, SqlProbeStatus.Ok, false, [], [], []);
        db.PerfRecordingSamples.Add(new PerfRecordingSample
        {
            Id = Guid.NewGuid(),
            RecordingId = id,
            SampleUtc = DateTime.UtcNow,
            Measuring = false,
            CpuPercent = 42,
            ProcessesInaccessible = 0,
            ProcessGroupsJson = JsonSerializer.Serialize(
                new[] { new ProcessGroupUsage("OneC", 40, 1_000_000, 3) }, PerfSampleJson.Options),
            OneCLoadJson = JsonSerializer.Serialize(onec, PerfSampleJson.Options),
            SqlLoadJson = JsonSerializer.Serialize(sql, PerfSampleJson.Options),
        });
        await db.SaveChangesAsync();

        var result = await PerformanceEndpoints.GetRecordingAsync(id, db, CancellationToken.None);

        var detail = result.Result.Should().BeOfType<Ok<RecordingDetail>>().Subject.Value!;
        detail.Recording.Id.Should().Be(id);
        detail.Samples.Should().ContainSingle();
        var sample = detail.Samples[0];
        sample.CpuPercent.Should().Be(42);
        sample.ProcessGroups.Should().ContainSingle().Which.Family.Should().Be("OneC");
        sample.OneC!.Sessions.Should().ContainSingle().Which.MemoryCurrent.Should().Be(-1138560);
        sample.Sql!.Status.Should().Be(SqlProbeStatus.Ok);
    }

    [Fact]
    public async Task GetRecording_missing_returns_not_found()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await PerformanceEndpoints.GetRecordingAsync(Guid.NewGuid(), db, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task DeleteRecording_removes_stopped_recording()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        db.PerfRecordings.Add(new PerfRecording
        {
            Id = id,
            StartedAtUtc = DateTime.UtcNow,
            StoppedAtUtc = DateTime.UtcNow,
            Status = PerfRecordingStatus.Stopped,
            StopReason = PerfRecordingStopReason.Manual,
            StartedBy = "admin",
        });
        await db.SaveChangesAsync();

        var result = await PerformanceEndpoints.DeleteRecordingAsync(id, db, CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();
        db.PerfRecordings.Count().Should().Be(0);
    }

    [Fact]
    public async Task DeleteRecording_active_returns_conflict()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        db.PerfRecordings.Add(new PerfRecording
        {
            Id = id,
            StartedAtUtc = DateTime.UtcNow,
            Status = PerfRecordingStatus.Active,
            StartedBy = "admin",
        });
        await db.SaveChangesAsync();

        var result = await PerformanceEndpoints.DeleteRecordingAsync(id, db, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.RecordingActive);
        db.PerfRecordings.Count().Should().Be(1, "идущую запись удалять нельзя — сначала остановить");
    }

    [Fact]
    public async Task DeleteRecording_missing_returns_not_found()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await PerformanceEndpoints.DeleteRecordingAsync(Guid.NewGuid(), db, CancellationToken.None);

        result.Result.Should().BeOfType<NotFound>();
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
