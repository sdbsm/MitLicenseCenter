using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-130 (BE-17): серверная пагинация списка записей быстродействия.
public sealed class RecordingsPaginationTests
{
    private static readonly DateTime BaseTime = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Page_2_with_size_5_returns_correct_window()
    {
        using var db = TestHelpers.NewInMemoryDb();
        for (var i = 0; i < 12; i++)
        {
            db.PerfRecordings.Add(MakeRecording(BaseTime.AddMinutes(i)));
        }
        await db.SaveChangesAsync();

        var result = await PerformanceEndpoints.ListRecordingsAsync(
            page: 2,
            pageSize: 5,
            db: db,
            ct: CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<RecordingsPagedResponse>>().Subject;
        ok.Value!.Total.Should().Be(12);
        ok.Value.Page.Should().Be(2);
        ok.Value.PageSize.Should().Be(5);
        ok.Value.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task Page_beyond_last_returns_empty_without_crash()
    {
        using var db = TestHelpers.NewInMemoryDb();
        db.PerfRecordings.Add(MakeRecording(BaseTime));
        await db.SaveChangesAsync();

        var result = await PerformanceEndpoints.ListRecordingsAsync(
            page: 99,
            pageSize: 25,
            db: db,
            ct: CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<RecordingsPagedResponse>>().Subject;
        ok.Value!.Items.Should().BeEmpty("страница за пределами данных — пустая, не крэш");
        ok.Value.Total.Should().Be(1);
    }

    [Fact]
    public async Task Missing_page_and_pageSize_defaults_to_1_and_25()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await PerformanceEndpoints.ListRecordingsAsync(
            page: null,
            pageSize: null,
            db: db,
            ct: CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<RecordingsPagedResponse>>().Subject;
        ok.Value!.Page.Should().Be(1);
        ok.Value.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task Total_equals_all_recordings_regardless_of_page()
    {
        using var db = TestHelpers.NewInMemoryDb();
        for (var i = 0; i < 7; i++)
        {
            db.PerfRecordings.Add(MakeRecording(BaseTime.AddMinutes(i)));
        }
        await db.SaveChangesAsync();

        var result = await PerformanceEndpoints.ListRecordingsAsync(
            page: 1,
            pageSize: 3,
            db: db,
            ct: CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<RecordingsPagedResponse>>().Subject;
        ok.Value!.Total.Should().Be(7, "total — всегда число всех записей, не текущей страницы");
        ok.Value.Items.Should().HaveCount(3);
    }

    private static PerfRecording MakeRecording(DateTime startedAt) => new()
    {
        Id = Guid.NewGuid(),
        StartedAtUtc = startedAt,
        Status = PerfRecordingStatus.Stopped,
        StopReason = PerfRecordingStopReason.Manual,
        StoppedAtUtc = startedAt.AddMinutes(5),
        StartedBy = "admin",
    };
}
