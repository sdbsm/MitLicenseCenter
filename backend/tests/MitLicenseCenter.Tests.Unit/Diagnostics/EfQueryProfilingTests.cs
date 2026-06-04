using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MitLicenseCenter.Infrastructure.Diagnostics;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Diagnostics;

// MLC-038 (PERF-02) — таблица истинности гейта профиля EF-команд без загрузки хоста.
// Главное доказательство границы: при выключенном флаге профиль и sensitive-логирование
// отключены, а sensitive невозможен без явного профиля (значения параметров не утекают).
public sealed class EfQueryProfilingTests
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p =>
                new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    [Fact]
    public void Disabled_by_default_when_config_empty()
    {
        // Дефолт — профиль выключен: LogTo не навешивается, прод-логи 1:1.
        var config = Config();
        EfQueryProfiling.IsEnabled(config).Should().BeFalse();
        EfQueryProfiling.IsSensitiveEnabled(config).Should().BeFalse();
    }

    [Fact]
    public void Enabled_when_flag_true()
    {
        var config = Config((EfQueryProfiling.EnabledKey, "true"));
        EfQueryProfiling.IsEnabled(config).Should().BeTrue();
    }

    [Fact]
    public void Sensitive_off_without_profile_even_when_its_flag_true()
    {
        // Sensitive невозможен без включённого профиля — значения параметров не пишутся.
        var config = Config((EfQueryProfiling.SensitiveKey, "true"));
        EfQueryProfiling.IsSensitiveEnabled(config).Should().BeFalse();
    }

    [Fact]
    public void Sensitive_off_when_profile_on_but_its_flag_absent()
    {
        // Профиль включён, но отдельный sensitive-opt-in не задан → по-прежнему false.
        var config = Config((EfQueryProfiling.EnabledKey, "true"));
        EfQueryProfiling.IsSensitiveEnabled(config).Should().BeFalse();
    }

    [Fact]
    public void Sensitive_on_only_when_both_flags_true()
    {
        var config = Config(
            (EfQueryProfiling.EnabledKey, "true"),
            (EfQueryProfiling.SensitiveKey, "true"));
        EfQueryProfiling.IsSensitiveEnabled(config).Should().BeTrue();
    }

    [Fact]
    public void Log_path_defaults_under_localappdata()
    {
        var config = Config();
        var path = EfQueryProfiling.ResolveLogPath(config);
        path.Should().EndWith(Path.Combine("MitLicenseCenter", "perf", "ef-profile.log"));
    }

    [Fact]
    public void Log_path_honours_explicit_override()
    {
        var config = Config((EfQueryProfiling.LogPathKey, @"C:\tmp\ef.log"));
        EfQueryProfiling.ResolveLogPath(config).Should().Be(@"C:\tmp\ef.log");
    }
}
