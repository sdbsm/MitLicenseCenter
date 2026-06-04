using FluentAssertions;
using MitLicenseCenter.Infrastructure.Clusters;
using MitLicenseCenter.Tools.PerfHarness;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.PerfHarness;

// MLC-039 (PERF-03): guard'ы чистой логики харнесса — генерация графа уважает доменные
// инварианты, а заглушка маршрутизирует rac-команды и рендерит парсибельный вывод. Сам
// dev-харнесс прод-кодом не является (см. csproj IsPublishable=false; Web на него не ссылается).
public sealed class PerfHarnessTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_respects_unique_indexes_and_one_to_one_publication()
    {
        var opts = new SeedOptions { Tenants = 20, Infobases = 50, Audit = 0, Sessions = 500 };

        var graph = SeedDataGenerator.Build(opts, FixedNow);

        graph.Tenants.Should().HaveCount(20);
        graph.Infobases.Should().HaveCount(50);
        graph.Publications.Should().HaveCount(50);

        // IX Tenants.Name (unique).
        graph.Tenants.Select(t => t.Name).Should().OnlyHaveUniqueItems();

        // IX_Infobases_TenantId_Name (unique per-tenant).
        graph.Infobases.Select(i => (i.TenantId, i.Name)).Should().OnlyHaveUniqueItems();

        // IX_Infobases_ClusterInfobaseId (globally unique).
        graph.Infobases.Select(i => i.ClusterInfobaseId).Should().OnlyHaveUniqueItems();

        // Publication 1:1 — каждая ссылается на свою инфобазу, без сирот и дублей.
        var infobaseIds = graph.Infobases.Select(i => i.Id).ToHashSet();
        graph.Publications.Select(p => p.InfobaseId).Should().OnlyHaveUniqueItems();
        graph.Publications.Should().OnlyContain(p => infobaseIds.Contains(p.InfobaseId));
    }

    [Fact]
    public void Build_makes_over_limit_tenants_actually_exceed_their_limit()
    {
        var opts = new SeedOptions { Tenants = 20, Infobases = 50, Audit = 0, Sessions = 500, OverLimitFraction = 0.30 };

        var graph = SeedDataGenerator.Build(opts, FixedNow);

        var tenantByCluster = graph.Infobases.ToDictionary(i => i.ClusterInfobaseId, i => i.TenantId);
        var consumedByTenant = graph.Scenario.Sessions
            .GroupBy(s => tenantByCluster[s.ClusterInfobaseId])
            .ToDictionary(g => g.Key, g => g.Count());

        var overLimit = graph.Tenants
            .Where(t => consumedByTenant.GetValueOrDefault(t.Id, 0) > t.MaxConcurrentLicenses)
            .ToList();

        // 30% от 20 = 6 over-limit тенантов → enforcement/kill-путь сработает под нагрузкой.
        overLimit.Should().HaveCountGreaterThanOrEqualTo(6);
        graph.Scenario.Sessions.Should().HaveCount(500);
        graph.Scenario.Sessions.Should().OnlyContain(s => s.AppId == "1CV8");
    }

    [Fact]
    public void Build_is_deterministic_for_a_fixed_seed()
    {
        var opts = new SeedOptions { Tenants = 5, Infobases = 12, Audit = 0, Sessions = 40, Seed = 1039 };

        var a = SeedDataGenerator.Build(opts, FixedNow);
        var b = SeedDataGenerator.Build(opts, FixedNow);

        a.Scenario.ClusterUuid.Should().Be(b.Scenario.ClusterUuid);
        a.Scenario.Sessions.Select(s => s.SessionId)
            .Should().Equal(b.Scenario.Sessions.Select(s => s.SessionId));
        a.Infobases.Select(i => i.ClusterInfobaseId)
            .Should().Equal(b.Infobases.Select(i => i.ClusterInfobaseId));
    }

    [Fact]
    public void EnumerateAuditLogs_yields_exactly_K_rows()
    {
        var opts = new SeedOptions { Tenants = 5, Infobases = 10, Audit = 1234, Sessions = 10 };
        var graph = SeedDataGenerator.Build(opts, FixedNow);
        var tenantIds = graph.Tenants.Select(t => t.Id).ToList();

        var rows = SeedDataGenerator.EnumerateAuditLogs(opts, tenantIds, FixedNow).ToList();

        rows.Should().HaveCount(1234);
        rows.Should().OnlyContain(r => r.Timestamp <= FixedNow);
        rows.Should().OnlyContain(r => r.TenantId == null || tenantIds.Contains(r.TenantId!.Value));
    }

    // expected — имя RacStub.RacCommand строкой: внутренний enum нельзя выставлять в public-сигнатуре теста.
    [Theory]
    [InlineData(new[] { "cluster", "list" }, "ClusterList")]
    [InlineData(new[] { "localhost:1545", "session", "list", "--cluster=x" }, "SessionList")]
    [InlineData(new[] { "session", "terminate", "--session=x" }, "SessionTerminate")]
    [InlineData(new[] { "infobase", "summary", "list", "--cluster=x" }, "InfobaseSummaryList")]
    [InlineData(new[] { "wibble" }, "Unknown")]
    public void Classify_routes_rac_arguments(string[] args, string expected)
        => RacStub.Classify(args).ToString().Should().Be(expected);

    [Fact]
    public void Render_session_list_round_trips_through_RacOutputParser()
    {
        var opts = new SeedOptions { Tenants = 3, Infobases = 6, Audit = 0, Sessions = 25 };
        var graph = SeedDataGenerator.Build(opts, FixedNow);

        var (exitCode, stdout) = RacStub.Render(RacStub.RacCommand.SessionList, graph.Scenario);

        exitCode.Should().Be(0);
        var records = RacOutputParser.Parse(stdout);
        records.Should().HaveCount(25);
        records.Should().OnlyContain(r => r.ContainsKey("session") && r.ContainsKey("infobase") && r.ContainsKey("app-id"));
    }

    [Fact]
    public void Render_session_terminate_is_killed_with_no_output()
    {
        var (exitCode, stdout) = RacStub.Render(RacStub.RacCommand.SessionTerminate, scenario: null);

        exitCode.Should().Be(0);
        stdout.Should().BeEmpty();
    }
}
