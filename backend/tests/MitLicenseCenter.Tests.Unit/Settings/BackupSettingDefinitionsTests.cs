using FluentAssertions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Settings;

// MLC-076 (ADR-27): каталог настроек бэкапа — kind/default/диапазоны зафиксированы тестом,
// чтобы случайная правка дефолта/диапазона не прошла молча (whitelist SettingDefinitions —
// единственный источник правды, сидер сидит из него же).
public sealed class BackupSettingDefinitionsTests
{
    [Fact]
    public void FolderPath_is_a_path_without_seeded_default()
    {
        var def = SettingDefinitions.All[SettingKey.BackupFolderPath];

        def.Kind.Should().Be(SettingValueKind.Path);
        def.IsSecret.Should().BeFalse();
        def.DefaultValue.Should().BeNull(
            "папка бэкапов зависит от инсталляции — оператор задаёт явно (паттерн OneC.RAS.ExePath)");
    }

    [Theory]
    [InlineData(SettingKey.BackupTtlHours, "24", 1, 8760)]
    [InlineData(SettingKey.BackupMaxParallel, "2", 1, 8)]
    [InlineData(SettingKey.BackupDiskSafetyMarginMb, "2048", 0, 1048576)]
    public void Numeric_backup_settings_have_expected_defaults_and_ranges(
        string key, string expectedDefault, int expectedMin, int expectedMax)
    {
        var def = SettingDefinitions.All[key];

        def.Kind.Should().Be(SettingValueKind.Number);
        def.IsSecret.Should().BeFalse();
        def.DefaultValue.Should().Be(expectedDefault);
        def.Min.Should().Be(expectedMin);
        def.Max.Should().Be(expectedMax);
        int.Parse(def.DefaultValue!, System.Globalization.CultureInfo.InvariantCulture)
            .Should().BeInRange(expectedMin, expectedMax, "дефолт обязан попадать в свой же диапазон");
    }
}
