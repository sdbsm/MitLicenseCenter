using FluentAssertions;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Infrastructure.Clusters;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Clusters;

// Tripwire: после Stage 5 PR 5.1 (ADR-16) cluster-адаптер — exactly one
// (RacExecutableRasClusterClient), без декораторов / fallback-маркеров /
// circuit-status reader'ов. Эти тесты падают, если кто-то молча возвращает
// удалённые типы — заставляет автора PR явно мотивировать revocation ADR-16
// перед коммитом.
public sealed class ClusterRegistrationGuardTests
{
    [Fact]
    public void RacExecutableRasClusterClient_implements_IClusterClient_directly()
    {
        typeof(RacExecutableRasClusterClient)
            .GetInterfaces()
            .Should().Contain(typeof(IClusterClient));
    }

    [Fact]
    public void Application_Clusters_namespace_does_not_contain_removed_types()
    {
        // Source assembly = тот, где живёт IClusterClient (Application).
        var clusterTypeNames = typeof(IClusterClient).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "MitLicenseCenter.Application.Clusters")
            .Select(t => t.Name)
            .ToList();

        // Удалены в PR 5.1:
        clusterTypeNames.Should().NotContain("IRasFallbackClusterClient");
        clusterTypeNames.Should().NotContain("ICircuitStatusReader");
        clusterTypeNames.Should().NotContain("CircuitStatus");

        // Должны остаться:
        clusterTypeNames.Should().Contain(nameof(IClusterClient));
        clusterTypeNames.Should().Contain(nameof(IRasHealthReader));
        clusterTypeNames.Should().Contain(nameof(RasHealthSnapshot));
    }

    [Fact]
    public void Infrastructure_Clusters_namespace_does_not_contain_removed_types()
    {
        var clusterTypeNames = typeof(RacExecutableRasClusterClient).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "MitLicenseCenter.Infrastructure.Clusters"
                     || t.Namespace == "MitLicenseCenter.Infrastructure.Clusters.Testing")
            .Select(t => t.Name)
            .ToList();

        // Удалены в PR 5.1:
        clusterTypeNames.Should().NotContain("OneCRestClusterClient");
        clusterTypeNames.Should().NotContain("ResilientClusterClient");
        clusterTypeNames.Should().NotContain("ClusterCircuitState");
        clusterTypeNames.Should().NotContain("StubRasClusterClient");

        // Должны остаться:
        clusterTypeNames.Should().Contain(nameof(RacExecutableRasClusterClient));
        clusterTypeNames.Should().Contain(nameof(RacOutputParser));
        clusterTypeNames.Should().Contain(nameof(SystemProcessRacRunner));
        clusterTypeNames.Should().Contain(nameof(RasHealthState));
        clusterTypeNames.Should().Contain(nameof(RasHealthProbingService));
    }
}
