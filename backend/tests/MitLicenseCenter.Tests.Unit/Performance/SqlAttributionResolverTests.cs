using FluentAssertions;
using MitLicenseCenter.Application.Performance;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Performance;

// MLC-068: чистая сшивка имён баз из DMV с инфобазами панели (database→Infobase→tenant).
// Гранулярность — база (SQL→сеанс→юзер невозможна, ADR-26). Регистронезависимо.
public sealed class SqlAttributionResolverTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");

    private static readonly IReadOnlyList<InfobaseDatabaseRef> Infobases =
    [
        new("mitpro", TenantA, "Клиент А", "Бухгалтерия"),
        new("test", TenantB, "Клиент Б", "Зарплата"),
    ];

    [Fact]
    public void Resolve_maps_database_to_tenant_and_infobase()
    {
        var result = SqlAttributionResolver.Resolve(["mitpro"], Infobases);

        var a = result.Should().ContainSingle().Subject;
        a.DatabaseName.Should().Be("mitpro");
        a.TenantId.Should().Be(TenantA);
        a.TenantName.Should().Be("Клиент А");
        a.InfobaseName.Should().Be("Бухгалтерия");
    }

    [Fact]
    public void Resolve_matches_database_name_case_insensitively()
    {
        // DMV отдаёт DB_NAME с регистром БД; Infobase.DatabaseName сопоставляется без учёта регистра.
        var result = SqlAttributionResolver.Resolve(["MITPRO"], Infobases);

        result.Should().ContainSingle().Which.TenantId.Should().Be(TenantA);
    }

    [Fact]
    public void Resolve_returns_null_tenant_for_unregistered_database()
    {
        // Системная/чужая база (master, tempdb, БД панели) — видна, но «ничья».
        var result = SqlAttributionResolver.Resolve(["master"], Infobases);

        var m = result.Should().ContainSingle().Subject;
        m.DatabaseName.Should().Be("master");
        m.TenantId.Should().BeNull();
        m.TenantName.Should().BeNull();
        m.InfobaseName.Should().BeNull();
    }

    [Fact]
    public void Resolve_deduplicates_names_case_insensitively()
    {
        var result = SqlAttributionResolver.Resolve(["mitpro", "MitPro", "test"], Infobases);

        result.Should().HaveCount(2);
        result.Select(r => r.TenantId).Should().BeEquivalentTo([TenantA, TenantB]);
    }

    [Fact]
    public void Resolve_ignores_blank_names()
    {
        var result = SqlAttributionResolver.Resolve(["", "  ", "mitpro"], Infobases);

        result.Should().ContainSingle().Which.DatabaseName.Should().Be("mitpro");
    }

    [Fact]
    public void Resolve_picks_first_infobase_deterministically_when_database_shared()
    {
        // Маловероятно (имена баз уникальны на сервере), но при дубле берём детерминированно
        // первую по имени инфобазы — не зависит от порядка входа.
        IReadOnlyList<InfobaseDatabaseRef> shared =
        [
            new("shared", TenantB, "Клиент Б", "Яков"),
            new("shared", TenantA, "Клиент А", "Авдей"),
        ];

        var result = SqlAttributionResolver.Resolve(["shared"], shared);

        result.Should().ContainSingle().Which.InfobaseName.Should().Be("Авдей");
    }

    [Fact]
    public void Resolve_returns_empty_when_no_database_names()
    {
        SqlAttributionResolver.Resolve([], Infobases).Should().BeEmpty();
    }
}
