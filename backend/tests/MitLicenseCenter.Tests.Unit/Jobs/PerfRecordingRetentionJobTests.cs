using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Tests.Unit.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-169: ретеншен записей «Быстродействия» — двухфазное батчевое удаление PerfRecordings
// (+ их PerfRecordingSamples) старше cutoff (now - 90д, срок зашит константой). Реальный
// реляционный провайдер (SQLite, FK on) — provider-portable ExecuteDelete транслируется и
// сюда, и на прод-MSSQL.
public sealed class PerfRecordingRetentionJobTests
{
    // RetentionDays=90 зашит в джобе; cutoff = Now - 90д = 2026-03-17 03:45.
    private static readonly DateTime Now = new(2026, 6, 15, 3, 45, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Deletes_old_recordings_with_samples_keeps_recent_and_active()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var oldStopped = Rec(Now.AddDays(-100), PerfRecordingStatus.Stopped);
        var recentStopped = Rec(Now.AddDays(-10), PerfRecordingStatus.Stopped);
        // Старая, но всё ещё активная — не трогаем (её сэмплит фоновый таймер).
        var oldActive = Rec(Now.AddDays(-100), PerfRecordingStatus.Active);

        using (var seed = sqlite.NewContext())
        {
            seed.PerfRecordings.AddRange(oldStopped, recentStopped, oldActive);
            seed.PerfRecordingSamples.AddRange(
                Sample(oldStopped.Id, Now.AddDays(-100)),
                Sample(oldStopped.Id, Now.AddDays(-100)),
                Sample(recentStopped.Id, Now.AddDays(-10)),
                Sample(oldActive.Id, Now.AddDays(-100)));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.PerfRecordings.Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([recentStopped.Id, oldActive.Id],
                "удаляются только терминальные записи старше cutoff; активная и свежая остаются");
        verify.PerfRecordingSamples.Select(x => x.RecordingId).Distinct().ToList()
            .Should().BeEquivalentTo([recentStopped.Id, oldActive.Id],
                "сэмплы старой записи удалены каскадно фазой 1, прочие целы");
    }

    [Fact]
    public async Task Deletes_all_old_samples_across_multiple_batches()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var oldStopped = Rec(Now.AddDays(-100), PerfRecordingStatus.Stopped);
        // 5001 > BatchSize(5000) → фаза 1 делает минимум два прохода по сэмплам.
        const int sampleCount = 5001;
        using (var seed = sqlite.NewContext())
        {
            seed.PerfRecordings.Add(oldStopped);
            for (var i = 0; i < sampleCount; i++)
                seed.PerfRecordingSamples.Add(Sample(oldStopped.Id, Now.AddDays(-100)));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.PerfRecordingSamples.Count().Should().Be(0, "все сэмплы старой записи удалены за несколько батчей");
        verify.PerfRecordings.Count().Should().Be(0, "фаза 2 удалила освободившуюся запись");
    }

    [Fact]
    public async Task Does_nothing_when_no_recordings_are_old_enough()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var recent = Rec(Now.AddDays(-10), PerfRecordingStatus.Stopped);
        using (var seed = sqlite.NewContext())
        {
            seed.PerfRecordings.Add(recent);
            seed.PerfRecordingSamples.Add(Sample(recent.Id, Now.AddDays(-10)));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.PerfRecordings.Count().Should().Be(1);
        verify.PerfRecordingSamples.Count().Should().Be(1);
    }

    // MLC-074 (регресс): на проде включён EnableRetryOnFailure. Джоба открывает транзакции
    // вручную внутри CreateExecutionStrategy().ExecuteAsync — навешиваем ретраящую стратегию,
    // воспроизводя прод-гард: до обёртки RunAsync падал бы InvalidOperationException.
    [Fact]
    public async Task Deletes_old_recordings_under_a_retrying_execution_strategy()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create(
            o => o.ExecutionStrategy(d => new TestHelpers.RetriesOnFailureExecutionStrategy(d)));

        var oldStopped = Rec(Now.AddDays(-100), PerfRecordingStatus.Stopped);
        var recent = Rec(Now.AddDays(-1), PerfRecordingStatus.Stopped);
        using (var seed = sqlite.NewContext())
        {
            seed.PerfRecordings.AddRange(oldStopped, recent);
            seed.PerfRecordingSamples.AddRange(
                Sample(oldStopped.Id, Now.AddDays(-100)),
                Sample(recent.Id, Now.AddDays(-1)));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.PerfRecordings.Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([recent.Id], "ретеншен отрабатывает и при ретраящей стратегии (MLC-074)");
    }

    private static PerfRecordingRetentionJob NewJob(Infrastructure.Persistence.AppDbContext db) =>
        new(db, TestHelpers.FixedClock(Now), NullLogger<PerfRecordingRetentionJob>.Instance);

    private static PerfRecording Rec(DateTime startedAtUtc, PerfRecordingStatus status) => new()
    {
        Id = Guid.NewGuid(),
        StartedAtUtc = startedAtUtc,
        StoppedAtUtc = status == PerfRecordingStatus.Active ? null : startedAtUtc.AddMinutes(5),
        Status = status,
        StopReason = status == PerfRecordingStatus.Stopped ? PerfRecordingStopReason.Manual : null,
        StartedBy = "tester",
    };

    private static PerfRecordingSample Sample(Guid recordingId, DateTime sampleUtc) => new()
    {
        Id = Guid.NewGuid(),
        RecordingId = recordingId,
        SampleUtc = sampleUtc,
    };
}
