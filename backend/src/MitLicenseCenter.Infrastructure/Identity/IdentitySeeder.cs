using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Identity;

public static partial class IdentitySeeder
{
    public const string DefaultAdminUserName = "admin";

    // Конфиг-ключ: путь к одноразовому файлу с паролем первого администратора, заданным
    // оператором в мастере GUI-установщика (ADR-31). Опционален — в тестах переопределяет путь
    // на temp-файл; в dev/ops файла по дефолтному ProgramData-пути нет → ветка random (как раньше).
    private const string InitialAdminPasswordFileKey = "Seed:InitialAdminPasswordFile";

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "============================================================\nСоздан первый администратор:\n  Логин:  {UserName}\n  Пароль: {Password}\nЗапишите пароль и смените его при первом входе.\n============================================================")]
    private static partial void LogSeededAdmin(ILogger logger, string userName, string password);

    // Пароль задан оператором в мастере — НЕ логируем его (оператор его и так знает).
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Первый администратор '{UserName}' создан с заданным оператором паролем.")]
    private static partial void LogSeededAdminWithOperatorPassword(ILogger logger, string userName);

    public static async Task EnsureSeededAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();

        // Миграции применимы только к реляционному провайдеру. Под in-memory
        // провайдером (интеграционные тесты через WebApplicationFactory<Program>,
        // где сидинг теперь выполняется синхронно в пайплайне старта хоста)
        // MigrateAsync бросает — пропускаем; схему там материализует сам InMemory.
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(ct).ConfigureAwait(false);
        }

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
        var configuration = sp.GetRequiredService<IConfiguration>();
        var passwordFilePath = ResolveInitialAdminPasswordFilePath(configuration);

        var anyUser = await userManager.Users.AnyAsync(ct).ConfigureAwait(false);
        if (anyUser)
        {
            // Сидинг не нужен (пользователи уже есть): не оставляем висящий секрет — если
            // одноразовый файл всё ещё лежит (повторный запуск/апгрейд), удаляем его best-effort.
            TryDeletePasswordFile(passwordFilePath);
            return;
        }

        // Пароль admin: задан оператором в мастере установщика (одноразовый файл, ADR-31) —
        // используем и удаляем его; иначе генерируем случайный и пишем в лог (fallback для
        // не-инсталляторных путей: dev, db-reset, ручной деплой).
        var operatorPassword = TryReadInitialAdminPassword(passwordFilePath);
        var password = operatorPassword ?? GenerateInitialPassword();

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

        if (operatorPassword is not null)
        {
            LogSeededAdminWithOperatorPassword(logger, DefaultAdminUserName);
        }
        else
        {
            LogSeededAdmin(logger, DefaultAdminUserName, password);
        }
    }

    // Путь к одноразовому файлу пароля: из конфига (Seed:InitialAdminPasswordFile) либо дефолт
    // %ProgramData%\MitLicenseCenter\initial-admin.secret (та же прод-конвенция, что и key ring).
    private static string ResolveInitialAdminPasswordFilePath(IConfiguration configuration)
    {
        var configured = configuration[InitialAdminPasswordFileKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MitLicenseCenter",
            "initial-admin.secret");
    }

    // Читает пароль из одноразового файла (если есть и непустой) и best-effort удаляет файл.
    // Пароль нигде не логируется. Возвращает null, если файла нет или он пуст → ветка random.
    private static string? TryReadInitialAdminPassword(string path)
    {
        string content;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            content = File.ReadAllText(path).Trim();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        // Файл прочитан — удаляем его (одноразовый контракт), даже если содержимое пустое.
        TryDeletePasswordFile(path);

        return content.Length == 0 ? null : content;
    }

    private static void TryDeletePasswordFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // best-effort: файл под ACL %ProgramData%\MitLicenseCenter, транзиентный.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // internal (не private): переиспользуется dev/ops-утилитой PerfHarness `reset-admin`
    // (см. InternalsVisibleTo в .csproj) — единый генератор сохраняет парити с парольной
    // политикой Identity, без второго источника.
    internal static string GenerateInitialPassword()
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
