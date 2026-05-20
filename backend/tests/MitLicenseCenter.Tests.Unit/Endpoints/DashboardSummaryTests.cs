using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

public sealed class DashboardSummaryTests
{
    private static IActiveSessionSnapshotStore MakeStore(IReadOnlyList<SnapshotSessionEntry> items, string source = "Rest")
    {
        var store = Substitute.For<IActiveSessionSnapshotStore>();
        store.Current().Returns(new SnapshotPayload(items, DateTime.UtcNow, TookMs: 5, Source: source));
        return store;
    }

    private static ICircuitStatusReader MakeCircuit(string state = "Closed", string adapter = "Rest", string? error = null)
    {
        var reader = Substitute.For<ICircuitStatusReader>();
        reader.GetStatus().Returns(new CircuitStatus(state, new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc), error, adapter));
        return reader;
    }

    private static SnapshotSessionEntry Session(Guid tenantId, string tenantName, string infobaseName, bool consumes = true)
    {
        return new SnapshotSessionEntry(
            SessionId: Guid.NewGuid(),
            ClusterInfobaseId: Guid.NewGuid(),
            TenantId: tenantId,
            TenantName: tenantName,
            InfobaseName: infobaseName,
            AppId: "1CV8C",
            UserName: "u",
            Host: "h",
            ConsumesLicense: consumes,
            StartedAtUtc: DateTime.UtcNow);
    }

    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    [Fact]
    public async Task Empty_database_yields_zeros_and_no_top_tenants()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var store = MakeStore([]);
        var circuit = MakeCircuit();

        var result = await DashboardEndpoints.SummaryAsync(db, store, circuit, NewCache(), CancellationToken.None);

        var body = ((Ok<DashboardSummaryResponse>)result).Value!;
        body.TenantsTotal.Should().Be(0);
        body.TenantsActive.Should().Be(0);
        body.InfobasesTotal.Should().Be(0);
        body.SessionsActiveTotal.Should().Be(0);
        body.LicensesConsumedTotal.Should().Be(0);
        body.LicensesAvailableTotal.Should().Be(0);
        body.TopTenantsByConsumption.Should().BeEmpty();
        body.Cluster.State.Should().Be("Closed");
        body.Cluster.ActiveAdapter.Should().Be("Rest");
    }

    [Fact]
    public async Task Top_tenants_ordered_by_percent_desc_then_consumed_desc()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var t1 = new Tenant { Id = Guid.NewGuid(), Name = "T1-small-high%", MaxConcurrentLicenses = 5, IsActive = true, CreatedAt = DateTime.UtcNow };
        var t2 = new Tenant { Id = Guid.NewGuid(), Name = "T2-big-mid%", MaxConcurrentLicenses = 100, IsActive = true, CreatedAt = DateTime.UtcNow };
        var t3 = new Tenant { Id = Guid.NewGuid(), Name = "T3-empty", MaxConcurrentLicenses = 10, IsActive = true, CreatedAt = DateTime.UtcNow };
        var t4 = new Tenant { Id = Guid.NewGuid(), Name = "T4-zerolimit", MaxConcurrentLicenses = 0, IsActive = true, CreatedAt = DateTime.UtcNow };
        var t5 = new Tenant { Id = Guid.NewGuid(), Name = "T5-mid%", MaxConcurrentLicenses = 20, IsActive = true, CreatedAt = DateTime.UtcNow };
        var t6 = new Tenant { Id = Guid.NewGuid(), Name = "T6-low%", MaxConcurrentLicenses = 50, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Tenants.AddRange(t1, t2, t3, t4, t5, t6);
        await db.SaveChangesAsync();

        var sessions = new List<SnapshotSessionEntry>();
        // T1: 4 / 5 = 80%
        sessions.AddRange(Enumerable.Range(0, 4).Select(_ => Session(t1.Id, t1.Name, "BP")));
        // T2: 50 / 100 = 50%
        sessions.AddRange(Enumerable.Range(0, 50).Select(_ => Session(t2.Id, t2.Name, "BP")));
        // T4: 3 / 0 → percent = 0 (div-by-zero guard) — НЕ должен попадать в топ выше T2
        sessions.AddRange(Enumerable.Range(0, 3).Select(_ => Session(t4.Id, t4.Name, "BP")));
        // T5: 5 / 20 = 25%
        sessions.AddRange(Enumerable.Range(0, 5).Select(_ => Session(t5.Id, t5.Name, "BP")));
        // T6: 10 / 50 = 20%
        sessions.AddRange(Enumerable.Range(0, 10).Select(_ => Session(t6.Id, t6.Name, "BP")));

        var store = MakeStore(sessions);

        var result = await DashboardEndpoints.SummaryAsync(db, store, MakeCircuit(), NewCache(), CancellationToken.None);
        var body = ((Ok<DashboardSummaryResponse>)result).Value!;

        body.TopTenantsByConsumption.Should().HaveCount(5);
        body.TopTenantsByConsumption.Select(r => r.TenantName).Should().ContainInOrder(
            "T1-small-high%",
            "T2-big-mid%",
            "T5-mid%",
            "T6-low%",
            "T4-zerolimit");

        body.TopTenantsByConsumption[0].Percent.Should().Be(80);
        body.TopTenantsByConsumption[1].Percent.Should().Be(50);
        body.TopTenantsByConsumption[^1].Percent.Should().Be(0); // div-by-zero защита
    }

    [Fact]
    public async Task Tiebreaker_same_percent_orders_by_consumed_desc()
    {
        using var db = TestHelpers.NewInMemoryDb();
        // Оба = 50%, но T-big имеет больше consumed → должен идти первым.
        var tSmall = new Tenant { Id = Guid.NewGuid(), Name = "T-small", MaxConcurrentLicenses = 10, IsActive = true, CreatedAt = DateTime.UtcNow };
        var tBig = new Tenant { Id = Guid.NewGuid(), Name = "T-big", MaxConcurrentLicenses = 100, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Tenants.AddRange(tSmall, tBig);
        await db.SaveChangesAsync();

        var sessions = new List<SnapshotSessionEntry>();
        sessions.AddRange(Enumerable.Range(0, 5).Select(_ => Session(tSmall.Id, tSmall.Name, "BP")));
        sessions.AddRange(Enumerable.Range(0, 50).Select(_ => Session(tBig.Id, tBig.Name, "BP")));

        var result = await DashboardEndpoints.SummaryAsync(db, MakeStore(sessions), MakeCircuit(), NewCache(), CancellationToken.None);
        var body = ((Ok<DashboardSummaryResponse>)result).Value!;

        body.TopTenantsByConsumption.Should().HaveCount(2);
        body.TopTenantsByConsumption[0].TenantName.Should().Be("T-big");
        body.TopTenantsByConsumption[1].TenantName.Should().Be("T-small");
        body.TopTenantsByConsumption[0].Percent.Should().Be(body.TopTenantsByConsumption[1].Percent);
    }

    [Fact]
    public async Task Aggregates_kpi_counts_correctly()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tActive = new Tenant { Id = Guid.NewGuid(), Name = "Active", MaxConcurrentLicenses = 10, IsActive = true, CreatedAt = DateTime.UtcNow };
        var tInactive = new Tenant { Id = Guid.NewGuid(), Name = "Inactive", MaxConcurrentLicenses = 50, IsActive = false, CreatedAt = DateTime.UtcNow };
        db.Tenants.AddRange(tActive, tInactive);

        // 2 инфобазы под активным тенантом
        db.Infobases.AddRange(
            new Infobase { Id = Guid.NewGuid(), TenantId = tActive.Id, Name = "BP", ClusterInfobaseId = Guid.NewGuid(), DatabaseServer = "s", DatabaseName = "n", Status = InfobaseStatus.Active, CreatedAt = DateTime.UtcNow },
            new Infobase { Id = Guid.NewGuid(), TenantId = tActive.Id, Name = "ZUP", ClusterInfobaseId = Guid.NewGuid(), DatabaseServer = "s", DatabaseName = "n", Status = InfobaseStatus.Active, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sessions = new List<SnapshotSessionEntry>
        {
            Session(tActive.Id, tActive.Name, "BP", consumes: true),
            Session(tActive.Id, tActive.Name, "BP", consumes: true),
            Session(tActive.Id, tActive.Name, "BP", consumes: false), // тонкий клиент: не лицензируемый
        };

        var result = await DashboardEndpoints.SummaryAsync(db, MakeStore(sessions), MakeCircuit(), NewCache(), CancellationToken.None);
        var body = ((Ok<DashboardSummaryResponse>)result).Value!;

        body.TenantsTotal.Should().Be(2);
        body.TenantsActive.Should().Be(1);
        body.InfobasesTotal.Should().Be(2);
        body.SessionsActiveTotal.Should().Be(3);
        body.LicensesConsumedTotal.Should().Be(2);
        // Сумма лимитов по активным = 10, consumed = 2 → available = 8.
        body.LicensesAvailableTotal.Should().Be(8);
    }

    [Fact]
    public async Task Cluster_status_propagates_to_response()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var circuit = MakeCircuit(state: "Open", adapter: "Ras", error: "REST unreachable");

        var result = await DashboardEndpoints.SummaryAsync(db, MakeStore([]), circuit, NewCache(), CancellationToken.None);
        var body = ((Ok<DashboardSummaryResponse>)result).Value!;

        body.Cluster.State.Should().Be("Open");
        body.Cluster.ActiveAdapter.Should().Be("Ras");
        body.Cluster.LastErrorMessage.Should().Be("REST unreachable");
    }

    [Fact]
    public async Task Cache_short_circuits_db_within_ttl()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "T", MaxConcurrentLicenses = 5, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var snapshotStore = Substitute.For<IActiveSessionSnapshotStore>();
        snapshotStore.Current().Returns(new SnapshotPayload([], DateTime.UtcNow, 1, "Rest"));
        var circuit = MakeCircuit();
        var cache = NewCache();

        await DashboardEndpoints.SummaryAsync(db, snapshotStore, circuit, cache, CancellationToken.None);
        await DashboardEndpoints.SummaryAsync(db, snapshotStore, circuit, cache, CancellationToken.None);

        // Внутри 5-секундного TTL второй вызов берёт ответ из кэша — snapshot.Current()
        // отрабатывает ровно один раз.
        snapshotStore.Received(1).Current();
    }
}
