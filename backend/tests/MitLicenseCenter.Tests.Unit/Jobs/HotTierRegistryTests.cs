using FluentAssertions;
using MitLicenseCenter.Infrastructure.Jobs;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

public sealed class HotTierRegistryTests
{
    [Fact]
    public void Promote_makes_tenant_hot_immediately()
    {
        var registry = new HotTierRegistry();
        var tenantId = Guid.NewGuid();

        registry.Promote(tenantId);

        registry.IsHot(tenantId).Should().BeTrue();
        registry.CurrentHotTenants().Should().Contain(tenantId);
    }

    [Fact]
    public void Demote_once_keeps_tenant_hot()
    {
        var registry = new HotTierRegistry();
        var tenantId = Guid.NewGuid();

        registry.Promote(tenantId);
        registry.Demote(tenantId); // 1st cold cycle below threshold

        registry.IsHot(tenantId).Should().BeTrue("one cold cycle is not enough to demote");
    }

    [Fact]
    public void Demote_twice_removes_tenant_from_hot_tier()
    {
        var registry = new HotTierRegistry();
        var tenantId = Guid.NewGuid();

        registry.Promote(tenantId);
        registry.Demote(tenantId); // 1st cold cycle
        registry.Demote(tenantId); // 2nd cold cycle → demote

        registry.IsHot(tenantId).Should().BeFalse("two consecutive cold cycles should demote");
        registry.CurrentHotTenants().Should().NotContain(tenantId);
    }

    [Fact]
    public void Promote_resets_cold_streak()
    {
        var registry = new HotTierRegistry();
        var tenantId = Guid.NewGuid();

        registry.Promote(tenantId);
        registry.Demote(tenantId); // 1st cold cycle
        registry.Promote(tenantId); // re-promote resets streak
        registry.Demote(tenantId); // 1st cold cycle again

        registry.IsHot(tenantId).Should().BeTrue("streak was reset by Promote");
    }

    [Fact]
    public void Demote_on_non_hot_tenant_is_noop()
    {
        var registry = new HotTierRegistry();
        var tenantId = Guid.NewGuid();

        registry.Demote(tenantId); // Not hot → nothing happens

        registry.IsHot(tenantId).Should().BeFalse();
        registry.CurrentHotTenants().Should().BeEmpty();
    }
}
