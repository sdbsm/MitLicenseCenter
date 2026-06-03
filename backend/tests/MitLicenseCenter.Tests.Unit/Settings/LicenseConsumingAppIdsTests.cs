using FluentAssertions;
using MitLicenseCenter.Application.Settings;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Settings;

// MLC-024: whitelist лицензионных app-id вынесен в dbo.Settings. Парсер обязан давать
// ровно дефолтный набор при пустом/незаданном значении (поведение 1:1 с прежним
// статическим HashSet) и читать кастомный список без учёта регистра/пробелов.
public sealed class LicenseConsumingAppIdsTests
{
    private static readonly string[] DefaultSet =
        ["1CV8", "1CV8C", "WebClient", "Designer", "COMConnection"];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    [InlineData(" , , ")]
    public void Parse_falls_back_to_default_when_empty_or_unset(string? raw)
    {
        var set = LicenseConsumingAppIds.Parse(raw);

        set.Should().BeEquivalentTo(DefaultSet);
    }

    [Fact]
    public void Parse_default_constant_yields_the_five_app_ids()
    {
        LicenseConsumingAppIds.Parse(LicenseConsumingAppIds.Default)
            .Should().BeEquivalentTo(DefaultSet);
    }

    [Fact]
    public void Parse_is_case_insensitive()
    {
        var set = LicenseConsumingAppIds.Parse("1cv8");

        set.Contains("1CV8").Should().BeTrue();
        set.Contains("1Cv8").Should().BeTrue();
    }

    [Fact]
    public void Parse_trims_whitespace_and_drops_empty_entries()
    {
        var set = LicenseConsumingAppIds.Parse("  BackgroundJob , RAS ,, WebClient  ");

        set.Should().BeEquivalentTo("BackgroundJob", "RAS", "WebClient");
    }

    [Fact]
    public void Parse_custom_list_replaces_default_entirely()
    {
        var set = LicenseConsumingAppIds.Parse("BackgroundJob");

        set.Should().ContainSingle().Which.Should().Be("BackgroundJob");
        set.Contains("1CV8").Should().BeFalse("кастомный список полностью заменяет дефолт");
    }
}
