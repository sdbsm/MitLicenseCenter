using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;
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

    // MLC-102: на чистой установке мастер кладёт пароль admin в одноразовый файл
    // (%ProgramData%\MitLicenseCenter\initial-admin.secret; в тесте путь переопределён
    // конфиг-ключом Seed:InitialAdminPasswordFile на temp-файл). Сидер использует пароль и
    // удаляет файл; если файла нет — random+EventId 1001 (fallback dev/db-reset/ручного деплоя).

    [Fact]
    public async Task EnsureSeeded_uses_operator_password_from_file_then_deletes_it()
    {
        var password = "Operator-Pwd-123!"; // валиден по политике (≥12, 4 класса)
        var file = WriteTempPasswordFile(password);
        try
        {
            await using var h = await SeederHarness.CreateAsync(file);

            await IdentitySeeder.EnsureSeededAsync(h.Services);

            var admin = await h.UserManager.FindByNameAsync(IdentitySeeder.DefaultAdminUserName);
            admin.Should().NotBeNull();
            (await h.UserManager.IsInRoleAsync(admin!, Roles.Admin)).Should().BeTrue();
            (await h.UserManager.CheckPasswordAsync(admin!, password)).Should().BeTrue();
            File.Exists(file).Should().BeFalse("одноразовый файл должен быть удалён после использования");
        }
        finally
        {
            TryDelete(file);
        }
    }

    [Fact]
    public async Task EnsureSeeded_without_file_generates_random_password()
    {
        // Файла нет → ветка random. Путь указываем на несуществующий temp-файл.
        var file = Path.Combine(Path.GetTempPath(), $"mlc-secret-{Guid.NewGuid():N}.secret");
        File.Exists(file).Should().BeFalse();

        await using var h = await SeederHarness.CreateAsync(file);

        await IdentitySeeder.EnsureSeededAsync(h.Services);

        var admin = await h.UserManager.FindByNameAsync(IdentitySeeder.DefaultAdminUserName);
        admin.Should().NotBeNull();
        (await h.UserManager.IsInRoleAsync(admin!, Roles.Admin)).Should().BeTrue();
        // Заданного «оператором» пароля файла не существовало — случайный сгенерированный его не примет.
        (await h.UserManager.CheckPasswordAsync(admin!, "Operator-Pwd-123!")).Should().BeFalse();
    }

    [Fact]
    public async Task EnsureSeeded_with_policy_violating_password_throws()
    {
        var weak = "short"; // < 12 символов, нарушает политику → fail-fast
        var file = WriteTempPasswordFile(weak);
        try
        {
            await using var h = await SeederHarness.CreateAsync(file);

            var act = async () => await IdentitySeeder.EnsureSeededAsync(h.Services);

            await act.Should().ThrowAsync<InvalidOperationException>();
            // Файл прочитан (одноразовый контракт) → удалён, даже когда пароль отвергнут.
            File.Exists(file).Should().BeFalse();
        }
        finally
        {
            TryDelete(file);
        }
    }

    [Fact]
    public async Task EnsureSeeded_when_user_exists_deletes_lingering_file_without_second_admin()
    {
        var file = WriteTempPasswordFile("Operator-Pwd-123!");
        try
        {
            await using var h = await SeederHarness.CreateAsync(file);

            // Первый прогон создаёт admin и удаляет файл.
            await IdentitySeeder.EnsureSeededAsync(h.Services);
            // Кладём файл повторно (имитация апгрейда/повторного запуска со «висящим» секретом).
            File.WriteAllText(file, "Another-Pwd-456!");

            await IdentitySeeder.EnsureSeededAsync(h.Services);

            File.Exists(file).Should().BeFalse("висящий секрет должен быть удалён, даже когда сидинг не нужен");
            h.UserManager.Users.Count().Should().Be(1, "второй администратор не создаётся");
        }
        finally
        {
            TryDelete(file);
        }
    }

    private static string WriteTempPasswordFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mlc-secret-{Guid.NewGuid():N}.secret");
        File.WriteAllText(path, content);
        return path;
    }

    private static void TryDelete(string path)
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
        }
    }

    // Реальный UserManager/RoleManager над EF InMemory + IConfiguration (с конфиг-ключом
    // Seed:InitialAdminPasswordFile). EnsureSeededAsync сам резолвит сервисы из провайдера и
    // создаёт scope, поэтому передаём корневой провайдер. InMemory — не реляционный, MigrateAsync
    // пропускается (Database.IsRelational() == false).
    private sealed class SeederHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public IServiceProvider Services => _provider;
        public UserManager<AppUser> UserManager { get; }

        private SeederHarness(ServiceProvider provider, IServiceScope scope, UserManager<AppUser> userManager)
        {
            _provider = provider;
            _scope = scope;
            UserManager = userManager;
        }

        public static Task<SeederHarness> CreateAsync(string passwordFilePath)
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Seed:InitialAdminPasswordFile"] = passwordFilePath,
                })
                .Build());
            services.AddDbContext<AppDbContext>(o =>
                o.UseInMemoryDatabase($"seeder-{Guid.NewGuid():N}"));
            services
                .AddIdentityCore<AppUser>(options =>
                {
                    options.User.RequireUniqueEmail = false;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                    options.Password.RequiredLength = 12;
                })
                .AddRoles<AppRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            return Task.FromResult(new SeederHarness(provider, scope, userManager));
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _provider.DisposeAsync();
        }
    }
}
