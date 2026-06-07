using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
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

// MLC-059 — обработчики AuthEndpoints, отвечающие за форс-смену пароля и «последний вход».
// Гоняем их напрямую (как UsersEndpoints-тесты), но поверх РЕАЛЬНОГО UserManager +
// SignInManager над EF InMemory с настоящей cookie-схемой Identity: PasswordSignInAsync и
// RefreshSignInAsync ходят в HttpContext.SignInAsync, поэтому харнесс поднимает
// AddAuthentication/AddCookie и подсовывает SignInManager общий HttpContext через
// IHttpContextAccessor — ровно как Web-слой в проде.
public sealed class AuthEndpointsTests
{
    private static readonly DateTime FixedNow = new(2026, 6, 8, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Login_success_writes_last_login_and_returns_must_change_flag()
    {
        await using var h = await AuthTestHarness.CreateAsync();
        await h.CreateUserAsync("operator", "Temp-Password-123!", mustChange: true);
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await AuthEndpoints.LoginAsync(
            new LoginRequest("operator", "Temp-Password-123!"),
            h.SignInManager, h.UserManager, audit, TestHelpers.FixedClock(FixedNow), CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<CurrentUserResponse>>().Subject;
        ok.Value!.MustChangePassword.Should().BeTrue("создан с временным паролем → форс-смена");

        var reloaded = (await h.UserManager.FindByNameAsync("operator"))!;
        reloaded.LastLoginAt.Should().Be(FixedNow);
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.AdminLoggedIn);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_unauthorized_and_no_last_login()
    {
        await using var h = await AuthTestHarness.CreateAsync();
        await h.CreateUserAsync("operator", "Temp-Password-123!", mustChange: false);

        var result = await AuthEndpoints.LoginAsync(
            new LoginRequest("operator", "wrong-password"),
            h.SignInManager, h.UserManager, new TestHelpers.CapturingAuditLogger(),
            TestHelpers.FixedClock(FixedNow), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedHttpResult>();
        (await h.UserManager.FindByNameAsync("operator"))!.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public async Task Me_returns_must_change_flag()
    {
        await using var h = await AuthTestHarness.CreateAsync();
        await h.CreateUserAsync("operator", "Temp-Password-123!", mustChange: true);
        h.SetCurrentUser("operator");

        var result = await AuthEndpoints.MeAsync(h.HttpContext, h.UserManager);

        var ok = result.Result.Should().BeOfType<Ok<CurrentUserResponse>>().Subject;
        ok.Value!.UserName.Should().Be("operator");
        ok.Value.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_clears_must_change_flag()
    {
        await using var h = await AuthTestHarness.CreateAsync();
        await h.CreateUserAsync("operator", "Temp-Password-123!", mustChange: true);
        h.SetCurrentUser("operator");
        var audit = new TestHelpers.CapturingAuditLogger();

        var result = await AuthEndpoints.ChangePasswordAsync(
            new ChangePasswordRequest("Temp-Password-123!", "New-Password-456!"),
            h.HttpContext, h.UserManager, h.SignInManager, audit, CancellationToken.None);

        result.Result.Should().BeOfType<NoContent>();

        var reloaded = (await h.UserManager.FindByNameAsync("operator"))!;
        reloaded.MustChangePassword.Should().BeFalse("успешная смена снимает требование форс-смены");
        (await h.UserManager.CheckPasswordAsync(reloaded, "New-Password-456!")).Should().BeTrue();
        audit.Entries.Should().ContainSingle(e => e.Action == AuditActionType.AdminPasswordChanged);
    }

    // Реальный UserManager + SignInManager над EF InMemory с cookie-схемой Identity и общим
    // HttpContext (через IHttpContextAccessor — его читают PasswordSignInAsync/RefreshSignInAsync).
    private sealed class AuthTestHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public UserManager<AppUser> UserManager { get; }
        public SignInManager<AppUser> SignInManager { get; }
        public DefaultHttpContext HttpContext { get; }

        private AuthTestHarness(
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

        public static async Task<AuthTestHarness> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
            services.AddDataProtection();
            // Plain-field accessor вместо штатного AsyncLocal: значение, выставленное в
            // CreateAsync, не «утекает» обратно к вызывающему тесту (AsyncLocal flows down,
            // не up), а SignInManager читает HttpContext именно через accessor.
            var accessor = new FieldHttpContextAccessor();
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddDbContext<AppDbContext>(o =>
                o.UseInMemoryDatabase($"auth-{Guid.NewGuid():N}"));
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
            services
                .AddAuthentication(IdentityConstants.ApplicationScheme)
                .AddCookie(IdentityConstants.ApplicationScheme);

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            foreach (var role in Roles.All)
            {
                await roleManager.CreateAsync(new AppRole(role));
            }

            // Общий HttpContext с RequestServices — его читает SignInManager через
            // IHttpContextAccessor для Context.SignInAsync (запись cookie).
            var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            accessor.HttpContext = httpContext;

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
            return new AuthTestHarness(provider, scope, userManager, signInManager, httpContext);
        }

        public async Task<AppUser> CreateUserAsync(string userName, string password, bool mustChange)
        {
            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                Email = null,
                EmailConfirmed = true,
                MustChangePassword = mustChange,
            };
            (await UserManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
            (await UserManager.AddToRoleAsync(user, Roles.Admin)).Succeeded.Should().BeTrue();
            return user;
        }

        public void SetCurrentUser(string userName) =>
            HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.Name, userName)], authenticationType: "Test"));

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
