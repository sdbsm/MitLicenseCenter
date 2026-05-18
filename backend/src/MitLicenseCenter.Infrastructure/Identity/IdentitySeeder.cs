using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Identity;

public static partial class IdentitySeeder
{
    public const string DefaultAdminUserName = "admin";

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "============================================================\nСоздан первый администратор:\n  Логин:  {UserName}\n  Пароль: {Password}\nЗапишите пароль и смените его при первом входе.\n============================================================")]
    private static partial void LogSeededAdmin(ILogger logger, string userName, string password);

    public static async Task EnsureSeededAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct).ConfigureAwait(false);

        var roleManager = sp.GetRequiredService<RoleManager<AppRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role).ConfigureAwait(false))
            {
                var result = await roleManager.CreateAsync(new AppRole(role)).ConfigureAwait(false);
                ThrowIfFailed(result, $"Не удалось создать роль '{role}'");
            }
        }

        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        var anyUser = await userManager.Users.AnyAsync(ct).ConfigureAwait(false);
        if (!anyUser)
        {
            var password = GenerateInitialPassword();
            var admin = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = DefaultAdminUserName,
                Email = null,
                EmailConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(admin, password).ConfigureAwait(false);
            ThrowIfFailed(createResult, "Не удалось создать первого администратора");

            var assignResult = await userManager.AddToRoleAsync(admin, Roles.Admin).ConfigureAwait(false);
            ThrowIfFailed(assignResult, $"Не удалось назначить роль '{Roles.Admin}' первому администратору");

            LogSeededAdmin(logger, DefaultAdminUserName, password);
        }
    }

    private static string GenerateInitialPassword()
    {
        // 24 символа из безопасного алфавита (без неоднозначных 0/O, 1/l/I).
        // Гарантируем минимум одну заглавную, одну строчную, одну цифру и один спецсимвол —
        // соответствует дефолтным требованиям ASP.NET Core Identity.
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*-_=+";
        const string all = upper + lower + digits + symbols;

        Span<char> buffer = stackalloc char[24];
        buffer[0] = PickChar(upper);
        buffer[1] = PickChar(lower);
        buffer[2] = PickChar(digits);
        buffer[3] = PickChar(symbols);
        for (var i = 4; i < buffer.Length; i++)
        {
            buffer[i] = PickChar(all);
        }

        // Перемешать (Fisher–Yates с криптослучайностью).
        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        return new string(buffer);
    }

    private static char PickChar(string alphabet) =>
        alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

    private static void ThrowIfFailed(IdentityResult result, string context)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new InvalidOperationException($"{context}. {errors}");
        }
    }
}
