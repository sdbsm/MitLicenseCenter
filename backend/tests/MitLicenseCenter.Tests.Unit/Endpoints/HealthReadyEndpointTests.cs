using System.Text.Json;
using FluentAssertions;
using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-040 (PERF-04): readiness-хендлер. Пробы дёргают только готовый снапшот
// IRasHealthReader (без спавна rac.exe), CanConnectAsync и Hangfire-сторадж.
public sealed class HealthReadyEndpointTests
{
    private static IRasHealthReader MakeRasHealth(
        bool healthy = true,
        DateTime? lastCheckedAtUtc = null,
        string? error = null)
    {
        var reader = Substitute.For<IRasHealthReader>();
        reader.GetSnapshot().Returns(new RasHealthSnapshot(
            Healthy: healthy,
            LastCheckedAtUtc: lastCheckedAtUtc ?? new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc),
            LastErrorMessage: error,
            ConsecutiveFailures: error is null ? 0 : 3));
        return reader;
    }

    private static JobStorage MakeHangfire(bool up = true)
    {
        var monitoring = Substitute.For<IMonitoringApi>();
        if (up)
        {
            monitoring.GetStatistics().Returns(new StatisticsDto());
        }
        else
        {
            monitoring.GetStatistics().Returns<StatisticsDto>(_ => throw new InvalidOperationException("hangfire storage down"));
        }

        var storage = Substitute.For<JobStorage>();
        storage.GetMonitoringApi().Returns(monitoring);
        return storage;
    }

    private static async Task<(int Http, ReadinessResponse Body)> InvokeAsync(
        AppDbContext db, IRasHealthReader ras, JobStorage hangfire)
    {
        var result = await HealthEndpoints.ReadyAsync(
            db, ras, hangfire, NullLoggerFactory.Instance, CancellationToken.None);

        return result.Result switch
        {
            Ok<ReadinessResponse> ok => (200, ok.Value!),
            JsonHttpResult<ReadinessResponse> json => (json.StatusCode ?? 200, json.Value!),
            _ => throw new InvalidOperationException("Неожиданный тип результата readiness."),
        };
    }

    [Fact]
    public async Task All_dependencies_up_yields_200_ready()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var (http, body) = await InvokeAsync(db, MakeRasHealth(), MakeHangfire());

        http.Should().Be(200);
        body.Status.Should().Be("ready");
        body.Checks.Database.Should().Be("ok");
        body.Checks.Ras.Should().Be("ok");
        body.Checks.Hangfire.Should().Be("ok");
    }

    [Fact]
    public async Task Ras_failure_yields_200_degraded_and_does_not_leak_error_text()
    {
        using var db = TestHelpers.NewInMemoryDb();
        const string secret = @"C:\Program Files\1cv8\rac.exe не найден на SRV-1С-01";

        var (http, body) = await InvokeAsync(db, MakeRasHealth(healthy: false, error: secret), MakeHangfire());

        http.Should().Be(200);
        body.Status.Should().Be("degraded");
        body.Checks.Ras.Should().Be("degraded");

        // Санитизация (ADR-4.1 / MLC-009): внутренний текст ошибки RAS не утекает в тело.
        var serialized = JsonSerializer.Serialize(body);
        serialized.Should().NotContain("rac.exe");
        serialized.Should().NotContain("SRV-1С-01");
    }

    [Fact]
    public async Task Ras_not_yet_probed_yields_unknown_and_ready()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var ras = Substitute.For<IRasHealthReader>();
        // LastCheckedAtUtc=null — первые 30с после старта («Проверка…»).
        ras.GetSnapshot().Returns(new RasHealthSnapshot(Healthy: true, LastCheckedAtUtc: null, LastErrorMessage: null, ConsecutiveFailures: 0));

        var (http, body) = await InvokeAsync(db, ras, MakeHangfire());

        http.Should().Be(200);
        body.Status.Should().Be("ready");
        body.Checks.Ras.Should().Be("unknown");
    }

    [Fact]
    public async Task Hangfire_storage_down_yields_200_degraded()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var (http, body) = await InvokeAsync(db, MakeRasHealth(), MakeHangfire(up: false));

        http.Should().Be(200);
        body.Status.Should().Be("degraded");
        body.Checks.Hangfire.Should().Be("down");
        body.Checks.Database.Should().Be("ok");
    }

    [Fact]
    public async Task Database_unreachable_yields_503_not_ready()
    {
        var db = TestHelpers.NewInMemoryDb();
        db.Dispose(); // disposed-контекст: CanConnectAsync бросает → суб-статус «down».

        var (http, body) = await InvokeAsync(db, MakeRasHealth(), MakeHangfire());

        http.Should().Be(503);
        body.Status.Should().Be("not_ready");
        body.Checks.Database.Should().Be("down");
    }
}
