using FluentAssertions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Settings;

// MLC-190: каталог настройки текста-причины принудительного завершения сеанса (rac
// terminate --error-message). Kind/секрет/непустой дефолт зафиксированы тестом, чтобы
// случайная правка не прошла молча (whitelist SettingDefinitions — единственный источник
// правды, сидер сидит из него же).
public sealed class EnforcementSettingDefinitionsTests
{
    [Fact]
    public void TerminateMessage_is_non_secret_text_with_seeded_default()
    {
        var def = SettingDefinitions.All[SettingKey.EnforcementTerminateMessage];

        def.Kind.Should().Be(SettingValueKind.Text);
        def.IsSecret.Should().BeFalse(
            "это не секрет — оператор видит и редактирует текст для пользователей 1С");
        def.DefaultValue.Should().NotBeNullOrWhiteSpace(
            "разумный RU-дефолт сидируется, чтобы при включении лимитов сообщение было осмысленным");
    }
}
