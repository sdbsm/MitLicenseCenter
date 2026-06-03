using FluentAssertions;
using MitLicenseCenter.Application.Sessions;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Sessions;

public sealed class LicenseConsumptionTests
{
    private static readonly DateTime BaseTime = new(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc);

    private static SnapshotSessionEntry Entry(
        Guid tenantId,
        bool consumes = true,
        DateTime? startedAt = null,
        Guid? sessionId = null) => new(
            SessionId: sessionId ?? Guid.NewGuid(),
            ClusterInfobaseId: Guid.NewGuid(),
            TenantId: tenantId,
            TenantName: "Acme",
            InfobaseName: "БП",
            AppId: "1CV8C",
            UserName: "user",
            Host: "WS01",
            ConsumesLicense: consumes,
            StartedAtUtc: startedAt ?? BaseTime);

    // ---- CountByTenant ----

    [Fact]
    public void CountByTenant_counts_only_license_consuming_sessions()
    {
        var tenant = Guid.NewGuid();
        var entries = new[]
        {
            Entry(tenant, consumes: true),
            Entry(tenant, consumes: true),
            Entry(tenant, consumes: false),
        };

        var result = LicenseConsumption.CountByTenant(entries);

        result.Should().ContainKey(tenant).WhoseValue.Should().Be(2);
    }

    [Fact]
    public void CountByTenant_groups_by_tenant()
    {
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var entries = new[]
        {
            Entry(t1), Entry(t1), Entry(t1),
            Entry(t2),
        };

        var result = LicenseConsumption.CountByTenant(entries);

        result[t1].Should().Be(3);
        result[t2].Should().Be(1);
    }

    [Fact]
    public void CountByTenant_omits_tenant_without_consuming_sessions()
    {
        var tenant = Guid.NewGuid();
        var entries = new[] { Entry(tenant, consumes: false) };

        var result = LicenseConsumption.CountByTenant(entries);

        result.Should().NotContainKey(tenant);
    }

    [Fact]
    public void CountByTenant_empty_input_yields_empty_dictionary()
    {
        var result = LicenseConsumption.CountByTenant(Array.Empty<SnapshotSessionEntry>());

        result.Should().BeEmpty();
    }

    // ---- FindOverLimit ----

    [Fact]
    public void FindOverLimit_includes_tenant_over_its_limit()
    {
        var tenant = Guid.NewGuid();
        var consumption = new Dictionary<Guid, int> { [tenant] = 5 };
        var limits = new Dictionary<Guid, int> { [tenant] = 3 };

        var result = LicenseConsumption.FindOverLimit(consumption, limits);

        result.Should().ContainSingle();
        result[0].Should().Be(new OverLimitTenant(tenant, Consumed: 5, Limit: 3));
    }

    [Theory]
    [InlineData(3, 3)] // consumed == limit → not over
    [InlineData(2, 3)] // consumed < limit  → not over
    public void FindOverLimit_excludes_tenant_at_or_under_limit(int consumed, int limit)
    {
        var tenant = Guid.NewGuid();
        var consumption = new Dictionary<Guid, int> { [tenant] = consumed };
        var limits = new Dictionary<Guid, int> { [tenant] = limit };

        var result = LicenseConsumption.FindOverLimit(consumption, limits);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FindOverLimit_excludes_tenant_with_non_positive_limit()
    {
        var tenant = Guid.NewGuid();
        var consumption = new Dictionary<Guid, int> { [tenant] = 5 };
        var limits = new Dictionary<Guid, int> { [tenant] = 0 };

        var result = LicenseConsumption.FindOverLimit(consumption, limits);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FindOverLimit_excludes_tenant_absent_from_limits_map()
    {
        // Tenant not in activeTenantLimits represents an inactive tenant.
        var tenant = Guid.NewGuid();
        var consumption = new Dictionary<Guid, int> { [tenant] = 5 };
        var limits = new Dictionary<Guid, int>();

        var result = LicenseConsumption.FindOverLimit(consumption, limits);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FindOverLimit_preserves_consumption_enumeration_order()
    {
        // Insertion order is the enumeration order for Dictionary without removals,
        // which the kill cap relies on.
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        var consumption = new Dictionary<Guid, int> { [t1] = 10, [t2] = 10, [t3] = 10 };
        var limits = new Dictionary<Guid, int> { [t1] = 1, [t2] = 1, [t3] = 1 };

        var result = LicenseConsumption.FindOverLimit(consumption, limits);

        result.Select(o => o.TenantId).Should().ContainInOrder(t1, t2, t3);
    }

    // ---- KillCandidates ----

    [Fact]
    public void KillCandidates_returns_only_target_tenant_consuming_sessions()
    {
        var target = Guid.NewGuid();
        var other = Guid.NewGuid();
        var entries = new[]
        {
            Entry(target, consumes: true),
            Entry(target, consumes: false),
            Entry(other, consumes: true),
        };

        var result = LicenseConsumption.KillCandidates(entries, target);

        result.Should().ContainSingle()
            .Which.TenantId.Should().Be(target);
    }

    [Fact]
    public void KillCandidates_orders_newest_first()
    {
        var tenant = Guid.NewGuid();
        var oldest = Entry(tenant, startedAt: BaseTime.AddMinutes(0));
        var middle = Entry(tenant, startedAt: BaseTime.AddMinutes(5));
        var newest = Entry(tenant, startedAt: BaseTime.AddMinutes(10));
        var entries = new[] { oldest, newest, middle };

        var result = LicenseConsumption.KillCandidates(entries, tenant);

        result.Select(e => e.SessionId)
            .Should().ContainInOrder(newest.SessionId, middle.SessionId, oldest.SessionId);
    }

    [Fact]
    public void KillCandidates_is_stable_for_equal_started_at()
    {
        var tenant = Guid.NewGuid();
        var first = Entry(tenant, startedAt: BaseTime);
        var second = Entry(tenant, startedAt: BaseTime);
        var third = Entry(tenant, startedAt: BaseTime);
        var entries = new[] { first, second, third };

        var result = LicenseConsumption.KillCandidates(entries, tenant);

        result.Select(e => e.SessionId)
            .Should().ContainInOrder(first.SessionId, second.SessionId, third.SessionId);
    }
}
