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

// MLC-058 — эндпоинты раздела «Пользователи» (переименован в MLC-060). Гоняем обработчики
// напрямую (как TenantsEndpoints-тесты), но поверх РЕАЛЬНОГО UserManager<AppUser> над EF
// InMemory: Identity-валидатор/хешер/token-провайдеры тогда 1:1 с приложением (lockout,
// дубликат логина, reset-token). Уникальный индекс логина InMemory не ставит, но дубликат
// ловит сам UserValidator (FindByNameAsync) — этот путь и проверяем.
public sealed class UsersEndpointsTests
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
        await using var h = await UserTestHarness.CreateAsync();
        var pwd = new CountingPasswordGenerator();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await UsersEndpoints.CreateAsync(
            new CreateUserRequest("operator", Roles.Admin),
            h.UserManager, pwd, audit, TestHelpers.NewHttpContext(), CancellationToken.None);

        var created = result.Result.Should().BeOfType<Created<UserCreatedResponse>>().Subject;
        created.Value!.UserName.Should().Be("operator");
        created.Value.GeneratedPassword.Should().Be("Aa1!_valid_pwd_0");

        var user = await h.UserManager.FindByNameAsync("operator");
        user.Should().NotBeNull();
        (await h.UserManager.IsInRoleAsync(user!, Roles.Admin)).Should().BeTrue();
        (await h.UserManager.CheckPasswordAsync(user!, "Aa1!_valid_pwd_0")).Should().BeTrue();
        // MLC-059 — выданный пароль временный: пользователь обязан сменить его при первом входе.
        user!.MustChangePassword.Should().BeTrue();

        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.UserCreated);
        audit.Entries[0].Description.Should().NotContain("Aa1!_valid_pwd_0", "пароль в аудит не пишется");
    }

    [Fact]
    public async Task Create_with_viewer_role_assigns_viewer()
    {
        await using var h = await UserTestHarness.CreateAsync();
        var result = await UsersEndpoints.CreateAsync(
            new CreateUserRequest("watcher", Roles.Viewer),
            h.UserManager, new CountingPasswordGenerator(), new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(), CancellationToken.None);

        result.Result.Should().BeOfType<Created<UserCreatedResponse>>();
        var user = await h.UserManager.FindByNameAsync("watcher");
        (await h.UserManager.IsInRoleAsync(user!, Roles.Viewer)).Should().BeTrue();
        (await h.UserManager.IsInRoleAsync(user!, Roles.Admin)).Should().BeFalse();
    }

    [Fact]
    public async Task Create_with_invalid_role_returns_validation_problem()
    {
        await using var h = await UserTestHarness.CreateAsync();
        var result = await UsersEndpoints.CreateAsync(
            new CreateUserRequest("x", "Root"),
            h.UserManager, new CountingPasswordGenerator(), new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(), CancellationToken.None);

        var problem = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey(nameof(CreateUserRequest.Role));
    }

    [Fact]
    public async Task Create_duplicate_username_returns_conflict()
    {
        await using var h = await UserTestHarness.CreateAsync();
        await h.CreateUserAsync("operator", Roles.Admin);

        var result = await UsersEndpoints.CreateAsync(
            new CreateUserRequest("operator", Roles.Admin),
            h.UserManager, new CountingPasswordGenerator(), new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(), CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.UserUsernameDuplicate);
    }

    [Fact]
    public async Task ResetPassword_sets_new_password_and_invalidates_old()
    {
        await using var h = await UserTestHarness.CreateAsync();
        var user = await h.CreateUserAsync("operator", Roles.Admin, "Old-Password-123!");
        var pwd = new CountingPasswordGenerator();
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await UsersEndpoints.ResetPasswordAsync(
            user.Id, h.UserManager, pwd, audit, TestHelpers.NewHttpContext(), CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<UserPasswordResetResponse>>().Subject;
        var newPwd = ok.Value!.GeneratedPassword;
        newPwd.Should().Be("Aa1!_valid_pwd_0");

        var reloaded = (await h.UserManager.FindByIdAsync(user.Id.ToString()))!;
        (await h.UserManager.CheckPasswordAsync(reloaded, newPwd)).Should().BeTrue();
        (await h.UserManager.CheckPasswordAsync(reloaded, "Old-Password-123!")).Should().BeFalse();
        // MLC-059 — сброшенный пароль временный: снова требуем смену при следующем входе.
        reloaded.MustChangePassword.Should().BeTrue();
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.UserPasswordReset);
    }

    [Fact]
    public async Task ResetPassword_unknown_id_returns_not_found()
    {
        await using var h = await UserTestHarness.CreateAsync();
        var result = await UsersEndpoints.ResetPasswordAsync(
            Guid.NewGuid(), h.UserManager, new CountingPasswordGenerator(),
            new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext(), CancellationToken.None);

        var nf = result.Result.Should().BeOfType<NotFound<ProblemDetails>>().Subject;
        nf.Value!.Extensions["code"].Should().Be(ProblemCodes.UserNotFound);
    }

    [Fact]
    public async Task Disable_then_enable_toggles_lockout()
    {
        await using var h = await UserTestHarness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);          // второй активный админ → guard не сработает
        var target = await h.CreateUserAsync("operator", Roles.Admin);
        var audit = new TestHelpers.CapturingAuditLogger();

        var disable = await UsersEndpoints.DisableAsync(
            target.Id, h.UserManager, audit, TestHelpers.NewHttpContext("keeper"),
            TimeProvider.System, CancellationToken.None);
        disable.Result.Should().BeOfType<NoContent>();

        var afterDisable = (await h.UserManager.FindByIdAsync(target.Id.ToString()))!;
        (await h.UserManager.IsLockedOutAsync(afterDisable)).Should().BeTrue("отключённая учётка не входит");

        var enable = await UsersEndpoints.EnableAsync(
            target.Id, h.UserManager, audit, TestHelpers.NewHttpContext("keeper"), CancellationToken.None);
        enable.Result.Should().BeOfType<NoContent>();

        var afterEnable = (await h.UserManager.FindByIdAsync(target.Id.ToString()))!;
        (await h.UserManager.IsLockedOutAsync(afterEnable)).Should().BeFalse();

        audit.Entries.Select(e => e.Action).Should()
            .Contain(new[] { AuditActionType.UserDisabled, AuditActionType.UserEnabled });
    }

    [Fact]
    public async Task Disable_last_active_admin_is_blocked()
    {
        await using var h = await UserTestHarness.CreateAsync();
        var onlyAdmin = await h.CreateUserAsync("operator", Roles.Admin);
        await h.CreateUserAsync("watcher", Roles.Viewer);        // Viewer не считается активным админом

        // Инициатор "ghost" не существует → self-guard пропускается, проверяем именно last-admin.
        var result = await UsersEndpoints.DisableAsync(
            onlyAdmin.Id, h.UserManager, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("ghost"), TimeProvider.System, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.UserLastActiveAdmin);
    }

    [Fact]
    public async Task Disable_self_is_blocked()
    {
        await using var h = await UserTestHarness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);          // другой активный админ есть → не last-admin
        var me = await h.CreateUserAsync("operator", Roles.Admin);

        var result = await UsersEndpoints.DisableAsync(
            me.Id, h.UserManager, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("operator"), TimeProvider.System, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.UserCannotDisableSelf);
    }

    // ── MLC-180 — жёсткое удаление учётки ──────────────────────────────────────────

    [Fact]
    public async Task Delete_removes_user_and_writes_audit()
    {
        await using var h = await UserTestHarness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);          // другой активный админ → guard не сработает
        var target = await h.CreateUserAsync("operator", Roles.Admin);
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await UsersEndpoints.DeleteAsync(
            target.Id, h.UserManager, audit, TestHelpers.NewHttpContext("keeper"),
            TimeProvider.System, CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();
        (await h.UserManager.FindByIdAsync(target.Id.ToString())).Should().BeNull("учётка удалена");

        var entry = audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.UserDeleted).Subject;
        entry.Initiator.Should().Be("keeper");
        entry.TenantId.Should().BeNull("операция server-scope — клиент не пишется");
        entry.Description.Should().Contain("operator");
    }

    [Fact]
    public async Task Delete_self_is_blocked()
    {
        await using var h = await UserTestHarness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);          // другой активный админ есть → не last-admin
        var me = await h.CreateUserAsync("operator", Roles.Admin);
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await UsersEndpoints.DeleteAsync(
            me.Id, h.UserManager, audit, TestHelpers.NewHttpContext("operator"),
            TimeProvider.System, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.UserCannotDisableSelf);
        (await h.UserManager.FindByIdAsync(me.Id.ToString())).Should().NotBeNull("отказ — учётка не удалена");
        audit.Entries.Should().NotContain(e => e.Action == AuditActionType.UserDeleted);
    }

    [Fact]
    public async Task Delete_last_active_admin_is_blocked()
    {
        await using var h = await UserTestHarness.CreateAsync();
        var onlyAdmin = await h.CreateUserAsync("operator", Roles.Admin);
        await h.CreateUserAsync("watcher", Roles.Viewer);        // Viewer не считается активным админом
        var audit = new TestHelpers.CapturingAuditLogger();

        // Инициатор "ghost" не существует → self-guard пропускается, проверяем именно last-admin.
        var result = await UsersEndpoints.DeleteAsync(
            onlyAdmin.Id, h.UserManager, audit, TestHelpers.NewHttpContext("ghost"),
            TimeProvider.System, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.UserLastActiveAdmin);
        (await h.UserManager.FindByIdAsync(onlyAdmin.Id.ToString())).Should().NotBeNull("отказ — учётка не удалена");
        audit.Entries.Should().NotContain(e => e.Action == AuditActionType.UserDeleted);
    }

    [Fact]
    public async Task Delete_unknown_id_returns_not_found()
    {
        await using var h = await UserTestHarness.CreateAsync();
        var result = await UsersEndpoints.DeleteAsync(
            Guid.NewGuid(), h.UserManager, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("keeper"), TimeProvider.System, CancellationToken.None);

        var nf = result.Result.Should().BeOfType<NotFound<ProblemDetails>>().Subject;
        nf.Value!.Extensions["code"].Should().Be(ProblemCodes.UserNotFound);
    }

    // ── MLC-061 — смена роли существующей учётки ───────────────────────────────────

    [Fact]
    public async Task ChangeRole_promotes_viewer_to_admin()
    {
        await using var h = await UserTestHarness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);          // инициатор ≠ target
        var target = await h.CreateUserAsync("watcher", Roles.Viewer);
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await UsersEndpoints.ChangeRoleAsync(
            target.Id, new ChangeUserRoleRequest(Roles.Admin), h.UserManager, audit,
            TestHelpers.NewHttpContext("keeper"), TimeProvider.System, CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        var reloaded = (await h.UserManager.FindByIdAsync(target.Id.ToString()))!;
        (await h.UserManager.IsInRoleAsync(reloaded, Roles.Admin)).Should().BeTrue();
        (await h.UserManager.IsInRoleAsync(reloaded, Roles.Viewer)).Should().BeFalse();
        var entry = audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.UserRoleChanged).Subject;
        entry.Description.Should().Contain("Viewer").And.Contain("Admin");
    }

    [Fact]
    public async Task ChangeRole_same_role_is_idempotent_without_audit()
    {
        await using var h = await UserTestHarness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);
        var target = await h.CreateUserAsync("watcher", Roles.Viewer);
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await UsersEndpoints.ChangeRoleAsync(
            target.Id, new ChangeUserRoleRequest(Roles.Viewer), h.UserManager, audit,
            TestHelpers.NewHttpContext("keeper"), TimeProvider.System, CancellationToken.None);

        result.Result.Should().BeOfType<Ok>();
        var reloaded = (await h.UserManager.FindByIdAsync(target.Id.ToString()))!;
        (await h.UserManager.IsInRoleAsync(reloaded, Roles.Viewer)).Should().BeTrue();
        audit.Entries.Should().NotContain(e => e.Action == AuditActionType.UserRoleChanged);
    }

    [Fact]
    public async Task ChangeRole_self_is_blocked()
    {
        await using var h = await UserTestHarness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);          // другой активный админ есть → не last-admin
        var me = await h.CreateUserAsync("operator", Roles.Admin);

        var result = await UsersEndpoints.ChangeRoleAsync(
            me.Id, new ChangeUserRoleRequest(Roles.Viewer), h.UserManager, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("operator"), TimeProvider.System, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.UserCannotChangeOwnRole);
    }

    [Fact]
    public async Task ChangeRole_demote_last_active_admin_is_blocked()
    {
        await using var h = await UserTestHarness.CreateAsync();
        var onlyAdmin = await h.CreateUserAsync("operator", Roles.Admin);
        await h.CreateUserAsync("watcher", Roles.Viewer);        // Viewer не считается активным админом

        // Инициатор "ghost" не существует → self-guard пропускается, проверяем именно last-admin.
        var result = await UsersEndpoints.ChangeRoleAsync(
            onlyAdmin.Id, new ChangeUserRoleRequest(Roles.Viewer), h.UserManager,
            new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext("ghost"),
            TimeProvider.System, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.UserLastActiveAdmin);
    }

    [Fact]
    public async Task ChangeRole_with_invalid_role_returns_validation_problem()
    {
        await using var h = await UserTestHarness.CreateAsync();
        var target = await h.CreateUserAsync("watcher", Roles.Viewer);

        var result = await UsersEndpoints.ChangeRoleAsync(
            target.Id, new ChangeUserRoleRequest("Root"), h.UserManager,
            new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext("keeper"),
            TimeProvider.System, CancellationToken.None);

        var problem = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey(nameof(ChangeUserRoleRequest.Role));
    }

    [Fact]
    public async Task ChangeRole_unknown_id_returns_not_found()
    {
        await using var h = await UserTestHarness.CreateAsync();
        var result = await UsersEndpoints.ChangeRoleAsync(
            Guid.NewGuid(), new ChangeUserRoleRequest(Roles.Admin), h.UserManager,
            new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext("keeper"),
            TimeProvider.System, CancellationToken.None);

        var nf = result.Result.Should().BeOfType<NotFound<ProblemDetails>>().Subject;
        nf.Value!.Extensions["code"].Should().Be(ProblemCodes.UserNotFound);
    }

    [Fact]
    public async Task List_returns_users_with_roles_and_status()
    {
        await using var h = await UserTestHarness.CreateAsync();
        await h.CreateUserAsync("alpha", Roles.Admin);
        var disabled = await h.CreateUserAsync("bravo", Roles.Viewer);
        await h.UserManager.SetLockoutEndDateAsync(disabled, DateTimeOffset.MaxValue);

        var result = await UsersEndpoints.ListAsync(h.UserManager, TimeProvider.System, CancellationToken.None);

        var items = result.Value!.Items;
        items.Should().HaveCount(2);
        items.Single(i => i.UserName == "alpha").Roles.Should().ContainSingle().Which.Should().Be(Roles.Admin);
        items.Single(i => i.UserName == "alpha").IsActive.Should().BeTrue();
        items.Single(i => i.UserName == "bravo").IsActive.Should().BeFalse();
        // MLC-059 — ни разу не входивший пользователь имеет пустой «последний вход».
        items.Single(i => i.UserName == "alpha").LastLoginAt.Should().BeNull();
    }

    // Реальный UserManager/RoleManager над EF InMemory + DataProtection (для reset-token).
    private sealed class UserTestHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public UserManager<AppUser> UserManager { get; }

        private UserTestHarness(ServiceProvider provider, IServiceScope scope, UserManager<AppUser> userManager)
        {
            _provider = provider;
            _scope = scope;
            UserManager = userManager;
        }

        public static async Task<UserTestHarness> CreateAsync()
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
            return new UserTestHarness(provider, scope, userManager);
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
