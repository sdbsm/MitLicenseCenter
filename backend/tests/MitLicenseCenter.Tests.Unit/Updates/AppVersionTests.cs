using FluentAssertions;
using MitLicenseCenter.Domain.Updates;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Updates;

// MLC-176 — чистый semver-компаратор канала обновлений (AppVersion + UpdateComparison).
public sealed class AppVersionTests
{
    [Theory]
    // current, latest, ожидается «доступно обновление»
    [InlineData("0.7.0", "0.7.0", false)]                 // равные
    [InlineData("0.7.0", "0.7.1", true)]                  // патч выше
    [InlineData("0.7.1", "0.7.0", false)]                 // патч ниже
    [InlineData("0.7.0", "0.8.0", true)]                  // minor выше
    [InlineData("0.8.0", "0.7.0", false)]                 // minor ниже
    [InlineData("0.7.0", "1.0.0", true)]                  // major выше
    [InlineData("1.0.0", "0.9.9", false)]                 // major ниже
    [InlineData("0.7.0-beta", "0.7.0", true)]             // release обходит свой prerelease
    [InlineData("0.7.0", "0.7.0-beta", false)]            // prerelease не обходит release
    [InlineData("0.7.0-beta", "0.7.0-beta", false)]       // оба prerelease при равной тройке → равны
    [InlineData("0.7.0-beta", "0.7.1-beta", true)]        // тройка решает, суффикс не важен
    [InlineData("v0.7.0", "v0.8.0", true)]                // ведущий v срезается с обеих сторон
    [InlineData("0.7.0", "v0.8.0", true)]                 // tag c v vs informational без v
    public void IsUpdateAvailable_compares_versions(string current, string latest, bool expected)
    {
        UpdateComparison.IsUpdateAvailable(current, latest).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("1.2")]           // не три компонента
    [InlineData("1.2.3.4")]       // больше трёх
    [InlineData("a.b.c")]
    public void IsUpdateAvailable_unparsable_side_yields_false(string? garbageLatest)
    {
        // Любая непарсимая сторона → false (без надёжной сверки баннер не показываем).
        UpdateComparison.IsUpdateAvailable("0.7.0", garbageLatest).Should().BeFalse();
        UpdateComparison.IsUpdateAvailable(garbageLatest, "9.9.9").Should().BeFalse();
    }

    [Fact]
    public void TryParse_strips_leading_v_and_detects_prerelease()
    {
        AppVersion.TryParse("v1.2.3-beta", out var value).Should().BeTrue();
        value.Should().Be(new AppVersion(1, 2, 3, IsPrerelease: true));
    }

    [Fact]
    public void TryParse_release_has_no_prerelease_flag()
    {
        AppVersion.TryParse("1.2.3", out var value).Should().BeTrue();
        value.IsPrerelease.Should().BeFalse();
    }

    [Fact]
    public void TryParse_null_or_garbage_returns_false()
    {
        AppVersion.TryParse(null, out _).Should().BeFalse();
        AppVersion.TryParse("garbage", out _).Should().BeFalse();
    }
}
