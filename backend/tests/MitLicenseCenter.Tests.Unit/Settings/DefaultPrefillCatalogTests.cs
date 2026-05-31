using FluentAssertions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Settings;

// ADR-17: три form-prefill ключа добавлены в общий каталог настроек. Они UI-only
// (бекенд их не читает), но обязаны быть в catalog'е, иначе PUT /settings/{key}
// вернёт 404 SETTING_UNKNOWN_KEY и оператор не сможет задать дефолты через UI.
public sealed class DefaultPrefillCatalogTests
{
    [Theory]
    [InlineData(SettingKey.DefaultsDatabaseServer)]
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
    public void DatabaseServer_and_PlatformVersion_have_no_seeded_default()
    {
        // Зависят от конкретной инсталляции — оператор задаёт явно через «Параметры».
        SettingDefinitions.All[SettingKey.DefaultsDatabaseServer].DefaultValue.Should().BeNull();
        SettingDefinitions.All[SettingKey.OneCDefaultPlatformVersion].DefaultValue.Should().BeNull();
    }
}
