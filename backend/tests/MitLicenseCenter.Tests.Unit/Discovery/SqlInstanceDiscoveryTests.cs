using FluentAssertions;
using MitLicenseCenter.Infrastructure.Discovery;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Discovery;

// Тест на чистый Map (имена инстансов → серверные строки), без реального реестра —
// Map не платформо-зависим, реестр читается отдельным [SupportedOSPlatform] методом.
public sealed class SqlInstanceDiscoveryTests
{
    [Fact]
    public void Map_default_instance_becomes_localhost()
    {
        string[] names = ["MSSQLSERVER"];

        var servers = SqlInstanceDiscovery.Map(names);

        servers.Should().Equal("localhost");
    }

    [Fact]
    public void Map_named_instance_becomes_localhost_backslash_name()
    {
        string[] names = ["SQLEXPRESS"];

        var servers = SqlInstanceDiscovery.Map(names);

        servers.Should().Equal(@"localhost\SQLEXPRESS");
    }

    [Fact]
    public void Map_default_is_case_insensitive()
    {
        string[] names = ["mssqlserver"];

        var servers = SqlInstanceDiscovery.Map(names);

        servers.Should().Equal("localhost");
    }

    [Fact]
    public void Map_deduplicates_instances_from_both_registry_views()
    {
        // Один и тот же инстанс прочитан из 64- и 32-битного view.
        string[] names = ["SQLEXPRESS", "SQLEXPRESS"];

        var servers = SqlInstanceDiscovery.Map(names);

        servers.Should().ContainSingle().Which.Should().Be(@"localhost\SQLEXPRESS");
    }

    [Fact]
    public void Map_sorts_and_keeps_default_first()
    {
        string[] names = ["SQLEXPRESS", "MSSQLSERVER", "DEV"];

        var servers = SqlInstanceDiscovery.Map(names);

        // "localhost" — префикс остальных, идёт первым; затем по алфавиту.
        servers.Should().Equal("localhost", @"localhost\DEV", @"localhost\SQLEXPRESS");
    }

    [Fact]
    public void Map_skips_blank_names()
    {
        string[] names = ["", "   ", "MSSQLSERVER"];

        var servers = SqlInstanceDiscovery.Map(names);

        servers.Should().Equal("localhost");
    }

    [Fact]
    public void Map_returns_empty_for_no_instances()
    {
        var servers = SqlInstanceDiscovery.Map(Array.Empty<string>());

        servers.Should().BeEmpty();
    }
}
