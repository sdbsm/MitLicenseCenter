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

    // ── Realistic-режим (демо-данные «как будто пользовались») ───────────────────────────

    [Fact]
    public void Realistic_perf_off_keeps_extreme_enforcement_limits()
    {
        var opts = new SeedOptions { Tenants = 50, Infobases = 100, Audit = 0, Sessions = 200 };

        var graph = SeedDataGenerator.Build(opts, FixedNow);

        // Perf-путь (Realistic=false) 1:1: лимиты строго 1 или 1_000_000, профили пусты.
        graph.Tenants.Select(t => t.MaxConcurrentLicenses).Should().OnlyContain(l => l == 1 || l == 1_000_000);
        graph.Profiles.Should().BeEmpty();
    }

    [Fact]
    public void Realistic_limits_are_smb_sized_in_three_bands()
    {
        var opts = new SeedOptions { Tenants = 100, Infobases = 300, Audit = 0, Realistic = true };

        var limits = SeedDataGenerator.Build(opts, FixedNow).Tenants
            .Select(t => t.MaxConcurrentLicenses).ToList();

        limits.Should().OnlyContain(l => l >= 5 && l <= 150, "СМБ-микс 5..150, не 1/1_000_000");
        limits.Should().Contain(l => l <= 20);                  // мелкие
        limits.Should().Contain(l => l >= 25 && l <= 60);       // средние
        limits.Should().Contain(l => l >= 75);                  // крупные
    }

    [Fact]
    public void Realistic_names_are_plausible_and_unique()
    {
        var opts = new SeedOptions { Tenants = 100, Infobases = 300, Audit = 0, Realistic = true };

        var names = SeedDataGenerator.Build(opts, FixedNow).Tenants.Select(t => t.Name).ToList();

        names.Should().OnlyHaveUniqueItems();
        names.Should().NotContain(n => n.StartsWith("perf-tenant", StringComparison.Ordinal));
        names.Should().OnlyContain(n => n.Contains('«', StringComparison.Ordinal));
    }

    [Fact]
    public void Realistic_profiles_couple_snapshot_limit_to_tenant_limit()
    {
        var opts = new SeedOptions { Tenants = 100, Infobases = 300, Audit = 0, Realistic = true };

        var graph = SeedDataGenerator.Build(opts, FixedNow);
        var limitById = graph.Tenants.ToDictionary(t => t.Id, t => t.MaxConcurrentLicenses);

        graph.Profiles.Should().HaveCount(100);
        graph.Profiles.Should().OnlyContain(p => p.Limit == limitById[p.TenantId],
            "snapshot.Limit = MaxConcurrentLicenses тенанта (как в проде ReconciliationJob)");
    }

    [Fact]
    public void Realistic_over_limit_tenants_peak_above_limit_normal_stay_below()
    {
        // OverLimitFraction = 0.10 — реалистичный дефолт CLI (Program.cs) для --realistic.
        var opts = new SeedOptions
        {
            Tenants = 100,
            Infobases = 300,
            Audit = 0,
            Realistic = true,
            OverLimitFraction = 0.10,
        };
        var graph = SeedDataGenerator.Build(opts, FixedNow);

        // Будни (2026-06-04 — четверг): сэмплируем каждый час суток.
        static int MaxOverDay(TenantUsageProfile p)
        {
            var maxConsumed = 0;
            for (var h = 0; h < 24; h++)
            {
                var s = SeedDataGenerator.BuildUsageSample(p.TenantIndex, p.Limit, p.OverLimit,
                    new DateTime(2026, 6, 4, h, 0, 0, DateTimeKind.Utc));
                maxConsumed = Math.Max(maxConsumed, s.ConsumedMax);
            }
            return maxConsumed;
        }

        foreach (var p in graph.Profiles.Where(p => p.OverLimit))
        {
            MaxOverDay(p).Should().BeGreaterThan(p.Limit, "over-limit тенант обязан пробивать лимит в пике");
        }

        foreach (var p in graph.Profiles.Where(p => !p.OverLimit))
        {
            MaxOverDay(p).Should().BeLessThanOrEqualTo(p.Limit, "normal тенант не превышает лимит");
        }

        // ~10% over-limit (выбор пользователя): clamp(ceil(100*0.10)) = 10.
        graph.Profiles.Count(p => p.OverLimit).Should().Be(10);
    }

    [Fact]
    public void Realistic_sessions_equal_current_consumption_and_consume_license()
    {
        var opts = new SeedOptions { Tenants = 100, Infobases = 300, Audit = 0, Realistic = true };
        var graph = SeedDataGenerator.Build(opts, FixedNow);

        // Сессий ровно столько, сколько суммарное текущее потребление профилей (у каждого ≥1 ИБ).
        var expected = graph.Profiles.Sum(p => p.CurrentConsumed);
        graph.Scenario.Sessions.Should().HaveCount(expected);
        graph.Scenario.Sessions.Should().OnlyContain(s => s.AppId == "1CV8");

        // «Постоянные» over-limit (чётный индекс) превышают лимит и «сейчас» (полночь) → красные/kill.
        graph.Profiles.Where(p => p.OverLimit && p.TenantIndex % 2 == 0)
            .Should().OnlyContain(p => p.CurrentConsumed > p.Limit);
    }

    [Fact]
    public void Realistic_is_deterministic_for_a_fixed_seed()
    {
        var opts = new SeedOptions { Tenants = 40, Infobases = 120, Audit = 0, Realistic = true, Seed = 1039 };

        var a = SeedDataGenerator.Build(opts, FixedNow);
        var b = SeedDataGenerator.Build(opts, FixedNow);

        a.Tenants.Select(t => t.MaxConcurrentLicenses).Should().Equal(b.Tenants.Select(t => t.MaxConcurrentLicenses));
        a.Tenants.Select(t => t.Name).Should().Equal(b.Tenants.Select(t => t.Name));
        a.Scenario.Sessions.Select(s => s.SessionId).Should().Equal(b.Scenario.Sessions.Select(s => s.SessionId));
    }
}
