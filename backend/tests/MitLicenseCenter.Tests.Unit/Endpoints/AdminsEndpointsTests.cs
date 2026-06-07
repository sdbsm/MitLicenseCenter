using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Identity;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-058 — эндпоинты раздела «Администраторы». Гоняем обработчики напрямую (как
// TenantsEndpoints-тесты), но поверх РЕАЛЬНОГО UserManager<AppUser> над EF InMemory:
// Identity-валидатор/хешер/token-провайдеры тогда 1:1 с приложением (lockout, дубликат
// логина, reset-token). Уникальный индекс логина InMemory не ставит, но дубликат ловит
// сам UserValidator (FindByNameAsync) — этот путь и проверяем.
public sealed class AdminsEndpointsTests
{
    // Детерминированный генератор: каждый вызов — новый валидный по политике пароль
    // (так пароль создания ≠ паролю сброса). Реальный InitialPasswordGenerator —
    // криптослучайный, для ассертов неудобен.
    private sealed class CountingPasswordGenerator : IInitialPasswordGenerator
    {
        private int _n;
        public string Generate() => $"Aa1!_valid_pwd_{_n++}";
    }

    [Fact]
    public async Task Create_creates_admin_with_role_and_returns_generated_password()
    {
        await using var h = await AdminTestHarness.CreateAsync();
        var pwd = new CountingPasswordGenerator();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await AdminsEndpoints.CreateAsync(
            new CreateAdminRequest("operator", Roles.Admin),
            h.UserManager, pwd, audit, TestHelpers.NewHttpContext(), CancellationToken.None);

        var created = result.Result.Should().BeOfType<Created<AdminCreatedResponse>>().Subject;
        created.Value!.UserName.Should().Be("operator");
        created.Value.GeneratedPassword.Should().Be("Aa1!_valid_pwd_0");

        var user = await h.UserManager.FindByNameAsync("operator");
        user.Should().NotBeNull();
        (await h.UserManager.IsInRoleAsync(user!, Roles.Admin)).Should().BeTrue();
        (await h.UserManager.CheckPasswordAsync(user!, "Aa1!_valid_pwd_0")).Should().BeTrue();
        // MLC-059 — выданный пароль временный: пользователь обязан сменить его при первом входе.
        user!.MustChangePassword.Should().BeTrue();

        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.AdminCreated);
        audit.Entries[0].Description.Should().NotContain("Aa1!_valid_pwd_0", "пароль в аудит не пишется");
    }

    [Fact]
    public async Task Create_with_viewer_role_assigns_viewer()
    {
        await using var h = await AdminTestHarness.CreateAsync();
        var result = await AdminsEndpoints.CreateAsync(
            new CreateAdminRequest("watcher", Roles.Viewer),
            h.UserManager, new CountingPasswordGenerator(), new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(), CancellationToken.None);

        result.Result.Should().BeOfType<Created<AdminCreatedResponse>>();
        var user = await h.UserManager.FindByNameAsync("watcher");
        (await h.UserManager.IsInRoleAsync(user!, Roles.Viewer)).Should().BeTrue();
        (await h.UserManager.IsInRoleAsync(user!, Roles.Admin)).Should().BeFalse();
    }

    [Fact]
    public async Task Create_with_invalid_role_returns_validation_problem()
    {
        await using var h = await AdminTestHarness.CreateAsync();
        var result = await AdminsEndpoints.CreateAsync(
            new CreateAdminRequest("x", "Root"),
            h.UserManager, new CountingPasswordGenerator(), new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(), CancellationToken.None);

        var problem = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey(nameof(CreateAdminRequest.Role));
    }

    [Fact]
    public async Task Create_duplicate_username_returns_conflict()
    {
        await using var h = await AdminTestHarness.CreateAsync();
        await h.CreateUserAsync("operator", Roles.Admin);

        var result = await AdminsEndpoints.CreateAsync(
            new CreateAdminRequest("operator", Roles.Admin),
            h.UserManager, new CountingPasswordGenerator(), new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.AdminUsernameDuplicate);
    }

    [Fact]
    public async Task ResetPassword_sets_new_password_and_invalidates_old()
    {
        await using var h = await AdminTestHarness.CreateAsync();
        var user = await h.CreateUserAsync("operator", Roles.Admin, "Old-Password-123!");
        var pwd = new CountingPasswordGenerator();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await AdminsEndpoints.ResetPasswordAsync(
            user.Id, h.UserManager, pwd, audit, TestHelpers.NewHttpContext(), CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<AdminPasswordResetResponse>>().Subject;
        var newPwd = ok.Value!.GeneratedPassword;
        newPwd.Should().Be("Aa1!_valid_pwd_0");

        var reloaded = (await h.UserManager.FindByIdAsync(user.Id.ToString()))!;
        (await h.UserManager.CheckPasswordAsync(reloaded, newPwd)).Should().BeTrue();
        (await h.UserManager.CheckPasswordAsync(reloaded, "Old-Password-123!")).Should().BeFalse();
        // MLC-059 — сброшенный пароль временный: снова требуем смену при следующем входе.
        reloaded.MustChangePassword.Should().BeTrue();
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.AdminPasswordReset);
    }

    [Fact]
    public async Task ResetPassword_unknown_id_returns_not_found()
    {
        await using var h = await AdminTestHarness.CreateAsync();
        var result = await AdminsEndpoints.ResetPasswordAsync(
            Guid.NewGuid(), h.UserManager, new CountingPasswordGenerator(),
            new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext(), CancellationToken.None);

        var nf = result.Result.Should().BeOfType<NotFound<ProblemDetails>>().Subject;
        nf.Value!.Extensions["code"].Should().Be(ProblemCodes.AdminNotFound);
    }

    [Fact]
    public async Task Disable_then_enable_toggles_lockout()
    {
        await using var h = await AdminTestHarness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);          // второй активный админ → guard не сработает
        var target = await h.CreateUserAsync("operator", Roles.Admin);
        var audit = new TestHelpers.CapturingAuditLogger();

        var disable = await AdminsEndpoints.DisableAsync(
            target.Id, h.UserManager, audit, TestHelpers.NewHttpContext("keeper"),
            TimeProvider.System, CancellationToken.None);
        disable.Result.Should().BeOfType<NoContent>();

        var afterDisable = (await h.UserManager.FindByIdAsync(target.Id.ToString()))!;
        (await h.UserManager.IsLockedOutAsync(afterDisable)).Should().BeTrue("отключённая учётка не входит");

        var enable = await AdminsEndpoints.EnableAsync(
            target.Id, h.UserManager, audit, TestHelpers.NewHttpContext("keeper"), CancellationToken.None);
        enable.Result.Should().BeOfType<NoContent>();

        var afterEnable = (await h.UserManager.FindByIdAsync(target.Id.ToString()))!;
        (await h.UserManager.IsLockedOutAsync(afterEnable)).Should().BeFalse();

        audit.Entries.Select(e => e.Action).Should()
            .Contain(new[] { AuditActionType.AdminDisabled, AuditActionType.AdminEnabled });
    }

    [Fact]
    public async Task Disable_last_active_admin_is_blocked()
    {
        await using var h = await AdminTestHarness.CreateAsync();
        var onlyAdmin = await h.CreateUserAsync("operator", Roles.Admin);
        await h.CreateUserAsync("watcher", Roles.Viewer);        // Viewer не считается активным админом

        // Инициатор "ghost" не существует → self-guard пропускается, проверяем именно last-admin.
        var result = await AdminsEndpoints.DisableAsync(
            onlyAdmin.Id, h.UserManager, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("ghost"), TimeProvider.System, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.AdminLastActive);
    }

    [Fact]
    public async Task Disable_self_is_blocked()
    {
        await using var h = await AdminTestHarness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);          // другой активный админ есть → не last-admin
        var me = await h.CreateUserAsync("operator", Roles.Admin);

        var result = await AdminsEndpoints.DisableAsync(
            me.Id, h.UserManager, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("operator"), TimeProvider.System, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.AdminCannotDisableSelf);
    }

    [Fact]
    public async Task List_returns_users_with_roles_and_status()
    {
        await using var h = await AdminTestHarness.CreateAsync();
        await h.CreateUserAsync("alpha", Roles.Admin);
        var disabled = await h.CreateUserAsync("bravo", Roles.Viewer);
        await h.UserManager.SetLockoutEndDateAsync(disabled, DateTimeOffset.MaxValue);

        var result = await AdminsEndpoints.ListAsync(h.UserManager, TimeProvider.System, CancellationToken.None);

        var items = result.Value!.Items;
        items.Should().HaveCount(2);
        items.Single(i => i.UserName == "alpha").Roles.Should().ContainSingle().Which.Should().Be(Roles.Admin);
        items.Single(i => i.UserName == "alpha").IsActive.Should().BeTrue();
        items.Single(i => i.UserName == "bravo").IsActive.Should().BeFalse();
        // MLC-059 — ни разу не входивший пользователь имеет пустой «последний вход».
        items.Single(i => i.UserName == "alpha").LastLoginAt.Should().BeNull();
    }

    // Реальный UserManager/RoleManager над EF InMemory + DataProtection (для reset-token).
    private sealed class AdminTestHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public UserManager<AppUser> UserManager { get; }

        private AdminTestHarness(ServiceProvider provider, IServiceScope scope, UserManager<AppUser> userManager)
        {
            _provider = provider;
            _scope = scope;
            UserManager = userManager;
        }

        public static async Task<AdminTestHarness> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
            services.AddDataProtection();
            services.AddDbContext<AppDbContext>(o =>
                o.UseInMemoryDatabase($"admins-{Guid.NewGuid():N}"));
            services
                .AddIdentityCore<AppUser>(options =>
                {
                    options.User.RequireUniqueEmail = false;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                    options.Password.RequiredLength = 12;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                })
                .AddRoles<AppRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            foreach (var role in Roles.All)
            {
                await roleManager.CreateAsync(new AppRole(role));
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            return new AdminTestHarness(provider, scope, userManager);
        }

        public async Task<AppUser> CreateUserAsync(string userName, string role, string password = "Seed-Password-123!")
        {
            var user = new AppUser { Id = Guid.NewGuid(), UserName = userName, Email = null, EmailConfirmed = true };
            (await UserManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            (await UserManager.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _provider.DisposeAsync();
        }
    }
}
