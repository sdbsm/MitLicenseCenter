using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MitLicenseCenter.Application.Identity;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-109 (SEC-01) — немедленный отзыв доступа при disable / reset-password / смене роли.
// Проверяем сквозной механизм: админ-операция ротирует security-stamp, а
// SecurityStampValidator (тот же, что подключён к OnValidatePrincipal в Program.cs) отвергает
// уже выданную куку с устаревшим stamp в пределах интервала ревалидации. Self-смена пароля —
// регрессия: RefreshSignInAsync переиздаёт куку свежим stamp'ом, своя сессия не рвётся.
//
// Гоняем поверх РЕАЛЬНЫХ UserManager + SignInManager над EF InMemory с настоящей
// cookie-схемой Identity и явной регистрацией ISecurityStampValidator/SecurityStampValidatorOptions
// — ровно как Infrastructure.DependencyInjection в проде (AddIdentityCore их сам НЕ ставит).
// ValidationInterval = Zero, чтобы валидатор сверял stamp на каждом прогоне без ожидания
// «настенных» 2 минут прод-интервала.
public sealed class SecurityStampRevocationTests
{
    [Fact]
    public async Task Disabled_user_cookie_is_rejected_by_validator_after_stamp_rotation()
    {
        await using var h = await Harness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);                  // второй активный админ → last-admin guard молчит
        var target = await h.CreateUserAsync("operator", Roles.Admin);

        // Кука, выданная ДО отключения: principal со «старым» stamp.
        var cookiePrincipal = await h.SignInManager.CreateUserPrincipalAsync(target);

        var disable = await UsersEndpoints.DisableAsync(
            target.Id, h.UserManager, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("keeper"), TimeProvider.System, CancellationToken.None);
        disable.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NoContent>();

        var rejected = await h.RunValidatorAsync(cookiePrincipal);
        rejected.Should().BeTrue("отключённый пользователь: старая кука отвергается валидатором");
    }

    [Fact]
    public async Task Demoted_admin_loses_admin_claims_cookie_without_relogin()
    {
        await using var h = await Harness.CreateAsync();
        await h.CreateUserAsync("keeper", Roles.Admin);                  // другой активный админ есть → не last-admin
        var target = await h.CreateUserAsync("operator", Roles.Admin);

        // Кука с Admin-claims, выданная ДО разжалования.
        var cookiePrincipal = await h.SignInManager.CreateUserPrincipalAsync(target);
        cookiePrincipal.IsInRole(Roles.Admin).Should().BeTrue("выдана с Admin-claims");

        var change = await UsersEndpoints.ChangeRoleAsync(
            target.Id, new ChangeUserRoleRequest(Roles.Viewer), h.UserManager,
            new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext("keeper"),
            TimeProvider.System, CancellationToken.None);
        change.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok>();

        var rejected = await h.RunValidatorAsync(cookiePrincipal);
        rejected.Should().BeTrue("разжалованный Admin: старая кука с Admin-claims отвергается валидатором");
    }

    [Fact]
    public async Task Reset_password_rejects_victim_cookie_after_stamp_rotation()
    {
        await using var h = await Harness.CreateAsync();
        var target = await h.CreateUserAsync("operator", Roles.Viewer, "Old-Password-123!");

        var cookiePrincipal = await h.SignInManager.CreateUserPrincipalAsync(target);

        var reset = await UsersEndpoints.ResetPasswordAsync(
            target.Id, h.UserManager, new CountingPasswordGenerator(),
            new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext("keeper"),
            CancellationToken.None);
        reset.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.Ok<UserPasswordResetResponse>>();

        var rejected = await h.RunValidatorAsync(cookiePrincipal);
        rejected.Should().BeTrue("сброс пароля ротирует stamp → старая кука отвергается валидатором");
    }

    [Fact]
    public async Task Self_change_password_keeps_own_session_alive()
    {
        await using var h = await Harness.CreateAsync();
        var me = await h.CreateUserAsync("operator", Roles.Admin, "Temp-Password-123!");
        h.SetCurrentUser("operator");
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await AuthEndpoints.ChangePasswordAsync(
            new ChangePasswordRequest("Temp-Password-123!", "New-Password-456!"),
            h.HttpContext, h.UserManager, h.SignInManager, audit, CancellationToken.None);
        result.Result.Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NoContent>();

        // Регрессия: после смены пароля собственная сессия живёт — переиздаём principal через
        // тот же SignInManager (как это делает RefreshSignInAsync, переписывая куку свежим
        // stamp'ом) и прогоняем через валидатор: он ДОЛЖЕН принять.
        var reloaded = (await h.UserManager.FindByNameAsync("operator"))!;
        var refreshedPrincipal = await h.SignInManager.CreateUserPrincipalAsync(reloaded);

        var rejected = await h.RunValidatorAsync(refreshedPrincipal);
        rejected.Should().BeFalse("self-смена пароля: своя сессия со свежим stamp остаётся валидной");
    }

    // Детерминированный генератор паролей сброса (как в UsersEndpointsTests).
    private sealed class CountingPasswordGenerator : IInitialPasswordGenerator
    {
        private int _n;
        public string Generate() => $"Aa1!_valid_pwd_{_n++}";
    }

    // Реальный UserManager + SignInManager над EF InMemory с cookie-схемой Identity, общим
    // HttpContext (через IHttpContextAccessor — его читают RefreshSignInAsync/валидатор) и
    // явной регистрацией SecurityStampValidator (Zero-интервал) — зеркало прод-DI.
    private sealed class Harness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public UserManager<AppUser> UserManager { get; }
        public SignInManager<AppUser> SignInManager { get; }
        public DefaultHttpContext HttpContext { get; }

        private Harness(
            ServiceProvider provider,
            IServiceScope scope,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            DefaultHttpContext httpContext)
        {
            _provider = provider;
            _scope = scope;
            UserManager = userManager;
            SignInManager = signInManager;
            HttpContext = httpContext;
        }

        public static async Task<Harness> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
            services.AddDataProtection();
            var accessor = new FieldHttpContextAccessor();
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddDbContext<AppDbContext>(o =>
                o.UseInMemoryDatabase($"secstamp-{Guid.NewGuid():N}"));
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
                .AddSignInManager()
                .AddDefaultTokenProviders();

            // Зеркало прод-DI (Infrastructure.DependencyInjection, MLC-109): AddIdentityCore
            // не регистрирует ни валидатор, ни его опции — ставим явно. Интервал зануляем,
            // чтобы stamp сверялся на каждом прогоне без ожидания прод-интервала (2 мин).
            services.AddScoped<ISecurityStampValidator, SecurityStampValidator<AppUser>>();
            services.AddScoped<ITwoFactorSecurityStampValidator, TwoFactorSecurityStampValidator<AppUser>>();
            services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);

            services
                .AddAuthentication(IdentityConstants.ApplicationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme)
                // Зеркало прод-Program.cs: валидатор при отклонении делает sign-out и по
                // TwoFactorRememberMe — без её регистрации SignOutAsync бросает.
                .AddCookie(IdentityConstants.TwoFactorRememberMeScheme);

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            foreach (var role in Roles.All)
            {
                await roleManager.CreateAsync(new AppRole(role));
            }

            var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            accessor.HttpContext = httpContext;

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
            return new Harness(provider, scope, userManager, signInManager, httpContext);
        }

        public async Task<AppUser> CreateUserAsync(string userName, string role, string password = "Seed-Password-123!")
        {
            var user = new AppUser { Id = Guid.NewGuid(), UserName = userName, Email = null, EmailConfirmed = true };
            (await UserManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            (await UserManager.AddToRoleAsync(user, role)).Succeeded.Should().BeTrue();
            return user;
        }

        public void SetCurrentUser(string userName) =>
            HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.Name, userName)], authenticationType: "Test"));

        // Прогоняет принципала куки через SecurityStampValidator ровно так, как cookie-пайплайн
        // в Program.cs (OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync).
        // Возвращает true, если валидатор отверг куку (RejectPrincipal → Principal == null).
        public async Task<bool> RunValidatorAsync(ClaimsPrincipal cookiePrincipal)
        {
            var scheme = new AuthenticationScheme(
                IdentityConstants.ApplicationScheme, IdentityConstants.ApplicationScheme,
                typeof(CookieAuthenticationHandler));
            var cookieOptions = _scope.ServiceProvider
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme);

            // IssuedUtc в прошлом → при ValidationInterval = Zero валидатор гарантированно
            // выполняет сверку stamp (не пропускает по «свежести» билета).
            var props = new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-5) };
            var ticket = new AuthenticationTicket(cookiePrincipal, props, IdentityConstants.ApplicationScheme);

            var context = new CookieValidatePrincipalContext(HttpContext, scheme, cookieOptions, ticket);
            await SecurityStampValidator.ValidatePrincipalAsync(context);

            return context.Principal is null;
        }

        private sealed class FieldHttpContextAccessor : IHttpContextAccessor
        {
            public HttpContext? HttpContext { get; set; }
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _provider.DisposeAsync();
        }
    }
}
