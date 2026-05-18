using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/auth")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Auth");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization();
        group.MapGet("/me", MeAsync).RequireAuthorization();
    }

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult, ValidationProblem>> LoginAsync(
        [FromBody] LoginRequest request,
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["Заполните имя пользователя и пароль."],
            });
        }

        var result = await signInManager.PasswordSignInAsync(
            request.UserName,
            request.Password,
            isPersistent: true,
            lockoutOnFailure: true).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByNameAsync(request.UserName).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        return TypedResults.Ok(new CurrentUserResponse(user.UserName!, roles.ToArray()));
    }

    private static async Task<NoContent> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> MeAsync(
        HttpContext httpContext,
        UserManager<AppUser> userManager)
    {
        var name = httpContext.User.Identity?.Name;
        if (string.IsNullOrEmpty(name))
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByNameAsync(name).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
        return TypedResults.Ok(new CurrentUserResponse(user.UserName!, roles.ToArray()));
    }
}
