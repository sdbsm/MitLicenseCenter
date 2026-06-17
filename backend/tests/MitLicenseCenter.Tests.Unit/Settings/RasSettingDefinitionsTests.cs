using FluentAssertions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Settings;

// MLC-194: каталог настройки порта агента кластера ragent (цель ras.exe при авто-
// регистрации службы RAS, ADR-47). Kind/секрет/дефолт/диапазон зафиксированы тестом,
// чтобы случайная правка не прошла молча (whitelist SettingDefinitions — единственный
// источник правды, сидер сидит из него же).
public sealed class RasSettingDefinitionsTests
{
    [Fact]
    public void AgentPort_is_non_secret_number_with_seeded_default_and_range()
    {
        var def = SettingDefinitions.All[SettingKey.OneCRasAgentPort];

        def.Kind.Should().Be(SettingValueKind.Number);
        def.IsSecret.Should().BeFalse("порт агента — не секрет");
        def.DefaultValue.Should().Be("1540", "стандартный порт агента кластера 1С");
        def.Min.Should().Be(1024);
        def.Max.Should().Be(65535);
    }
}
