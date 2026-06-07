using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MitLicenseCenter.Infrastructure;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Tools.PerfHarness;

// MLC-053 — dev/ops-утилита сброса пароля администратора без потери данных.
// Поднимает тот же DI-граф, что и приложение (AddInfrastructure), и резолвит UserManager<AppUser>
// из scope БЕЗ запуска хоста (Run/StartAsync не зовём → хостед-сервисы, IIS-адаптеры и RAS-пробер
// конструируются лениво и не стартуют). Так парольная политика и token-провайдеры — 1:1 с приложением.
internal static class AdminReset
{
    // Коды возврата: 0 — успех; 2 — пользователь не найден; 3 — сброс отклонён (политика/токен).
    public static async Task<int> RunAsync(
        string userName,
        string? password,
        bool unlock,
        string connectionString,
        TextWriter output,
        CancellationToken ct)
    {
        var builder = Host.CreateApplicationBuilder();

        // Строка подключения добавляется последним источником → перекрывает appsettings/env,
        // т.к. AddInfrastructure читает её через GetConnectionString("Default").
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
        });

        builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

        // AddInfrastructure регистрирует SignInManager (нужен IAuthenticationSchemeProvider из Web-слоя)
        // и хостед-сервисы. Нам нужен только UserManager, поэтому отключаем eager-валидацию контейнера
        // (в Development она включена по умолчанию и падала бы на SignInManager). Scope создаём вручную.
        builder.ConfigureContainer(new DefaultServiceProviderFactory(
            new ServiceProviderOptions { ValidateScopes = false, ValidateOnBuild = false }));

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = await userManager.FindByNameAsync(userName).ConfigureAwait(false);
        if (user is null)
        {
            output.WriteLine($"Пользователь '{userName}' не найден.");
            return 2;
        }

        // Пароль: явный --password, иначе криптослучайный, удовлетворяющий политике (единый генератор).
        var newPassword = password ?? IdentitySeeder.GenerateInitialPassword();

        // Сброс через штатный поток Identity (корректный хеш + проверка политики), не голым SQL.
        var token = await userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
        var resetResult = await userManager.ResetPasswordAsync(user, token, newPassword).ConfigureAwait(false);
        if (!resetResult.Succeeded)
        {
            output.WriteLine("Не удалось сбросить пароль:");
            foreach (var error in resetResult.Errors)
            {
                output.WriteLine($"  {error.Code}: {error.Description}");
            }
            return 3;
        }

        if (unlock)
        {
            await userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
            await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
        }

        // Логин и итоговый пароль — только в stdout (как сидер), не в файловые приёмники.
        output.WriteLine("============================================================");
        output.WriteLine("Пароль администратора сброшен:");
        output.WriteLine($"  Логин:  {userName}");
        output.WriteLine($"  Пароль: {newPassword}");
        if (unlock)
        {
            output.WriteLine("  Блокировка (lockout) снята.");
        }
        output.WriteLine("Запишите пароль и смените его при первом входе.");
        output.WriteLine("============================================================");
        return 0;
    }
}
