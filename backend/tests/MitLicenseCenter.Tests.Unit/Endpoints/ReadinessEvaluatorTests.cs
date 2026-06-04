using FluentAssertions;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-040 (PERF-04): матрица агрегации суб-статусов в overall + HTTP-код.
// БД гейтит not_ready/503; RAS-«Сбой» и Hangfire-down — только degraded/200;
// RAS-«unknown» (ещё не пробовали) не понижает overall.
public sealed class ReadinessEvaluatorTests
{
    [Theory]
    // database, ras, hangfire => overall, http
    [InlineData("ok", "ok", "ok", "ready", 200)]
    [InlineData("ok", "unknown", "ok", "ready", 200)]
    [InlineData("ok", "degraded", "ok", "degraded", 200)]
    [InlineData("ok", "ok", "down", "degraded", 200)]
    [InlineData("ok", "degraded", "down", "degraded", 200)]
    [InlineData("ok", "unknown", "down", "degraded", 200)]
    [InlineData("down", "ok", "ok", "not_ready", 503)]
    [InlineData("down", "degraded", "down", "not_ready", 503)]
    public void Evaluate_maps_subchecks_to_overall_and_http(
        string database, string ras, string hangfire, string expectedOverall, int expectedHttp)
    {
        var (overall, http) = ReadinessEvaluator.Evaluate(database, ras, hangfire);

        overall.Should().Be(expectedOverall);
        http.Should().Be(expectedHttp);
    }
}
