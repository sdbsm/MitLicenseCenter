using FluentAssertions;
using MitLicenseCenter.Infrastructure.Identity;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Identity;

// MLC-053: генератор пароля переиспользуется dev/ops-утилитой `reset-admin`, поэтому он —
// единственный источник парити с парольной политикой Identity (RequiredLength=12,
// Require Upper/Lower/Digit/NonAlphanumeric из AddInfrastructure). Тест стережёт это парити.
public sealed class IdentitySeederTests
{
    [Fact]
    public void GenerateInitialPassword_satisfies_identity_policy()
    {
        // Прогон много раз: генератор перемешивает символы криптослучайно — берём защиту от флака.
        for (var i = 0; i < 200; i++)
        {
            var password = IdentitySeeder.GenerateInitialPassword();

            password.Length.Should().BeGreaterThanOrEqualTo(12);
            password.Any(char.IsUpper).Should().BeTrue("политика требует заглавную");
            password.Any(char.IsLower).Should().BeTrue("политика требует строчную");
            password.Any(char.IsDigit).Should().BeTrue("политика требует цифру");
            password.Any(c => !char.IsLetterOrDigit(c)).Should().BeTrue("политика требует спецсимвол");
        }
    }

    [Fact]
    public void GenerateInitialPassword_produces_distinct_values()
    {
        var first = IdentitySeeder.GenerateInitialPassword();
        var second = IdentitySeeder.GenerateInitialPassword();

        first.Should().NotBe(second);
    }
}
