using MitLicenseCenter.Application.Identity;

namespace MitLicenseCenter.Infrastructure.Identity;

// MLC-058 — реализация порта поверх существующего генератора сидера. Та же сборка,
// поэтому internal IdentitySeeder.GenerateInitialPassword() доступен напрямую: единый
// источник генерации сохраняет парити с парольной политикой Identity (без дубля логики).
internal sealed class InitialPasswordGenerator : IInitialPasswordGenerator
{
    public string Generate() => IdentitySeeder.GenerateInitialPassword();
}
