using FluentAssertions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Settings;

// ADR-17: non-secret text-ключи без сидируемого дефолта в общем каталоге настроек.
// Sql.Server (MLC-087) — единственное место правды SQL-инстанса (читается бекендом);
// SiteName/PlatformVersion — form-prefill (UI-only). Все обязаны быть в catalog'е, иначе
// PUT /settings/{key} вернёт 404 SETTING_UNKNOWN_KEY и оператор не сможет их задать через UI.
public sealed class DefaultPrefillCatalogTests
{
    [Theory]
    [InlineData(SettingKey.SqlServer)]
    [InlineData(SettingKey.IisDefaultSiteName)]
    [InlineData(SettingKey.OneCDefaultPlatformVersion)]
    public void Form_prefill_key_is_in_catalog_and_is_a_non_secret_text_setting(string key)
    {
        SettingDefinitions.All.Should().ContainKey(key);
        var def = SettingDefinitions.All[key];
        def.IsSecret.Should().BeFalse();
        def.Kind.Should().Be(SettingValueKind.Text);
    }

    [Fact]
    public void IisDefaultSiteName_seeds_Default_Web_Site()
    {
        SettingDefinitions.All[SettingKey.IisDefaultSiteName].DefaultValue.Should().Be("Default Web Site");
    }

    [Fact]
    public void SqlServer_and_PlatformVersion_have_no_seeded_default()
    {
        // Зависят от конкретной инсталляции — оператор задаёт явно через «Параметры».
        SettingDefinitions.All[SettingKey.SqlServer].DefaultValue.Should().BeNull();
        SettingDefinitions.All[SettingKey.OneCDefaultPlatformVersion].DefaultValue.Should().BeNull();
    }
}
