using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Domain.Audit;
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
        group.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization();
    }

    internal static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult, ValidationProblem>> LoginAsync(
        [FromBody] LoginRequest request,
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        IAuditLogger audit,
        TimeProvider clock,
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

        // MLC-059 — фиксируем время успешного входа (UTC) для колонки «Последний вход»
        // в разделе «Администраторы».
        user.LastLoginAt = clock.GetUtcNow().UtcDateTime;
        await userManager.UpdateAsync(user).ConfigureAwait(false);

        await audit.LogAsync(
            AuditActionType.AdminLoggedIn,
            initiator: user.UserName!,
            description: $"Администратор {user.UserName} вошёл в систему.",
            ct: ct).ConfigureAwait(false);

        return TypedResults.Ok(new CurrentUserResponse(user.UserName!, roles.ToArray(), user.MustChangePassword));
    }

    private static async Task<NoContent> LogoutAsync(
        HttpContext httpContext,
        IAuditLogger audit,
        CancellationToken ct)
    {
        var name = httpContext.User.Identity?.Name;
        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(name))
        {
            await audit.LogAsync(
                AuditActionType.AdminLoggedOut,
                initiator: name,
                description: $"Администратор {name} вышел из системы.",
                ct: ct).ConfigureAwait(false);
        }

        return TypedResults.NoContent();
    }

    internal static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> MeAsync(
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
        return TypedResults.Ok(new CurrentUserResponse(user.UserName!, roles.ToArray(), user.MustChangePassword));
    }

    internal static async Task<Results<NoContent, UnauthorizedHttpResult, ValidationProblem>> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        HttpContext httpContext,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IAuditLogger audit,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            AddError(errors, nameof(request.CurrentPassword), "Укажите текущий пароль.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            AddError(errors, nameof(request.NewPassword), "Укажите новый пароль.");
        }
        else if (request.NewPassword.Length < 12)
        {
            AddError(errors, nameof(request.NewPassword), "Новый пароль должен быть не короче 12 символов.");
        }

        if (!string.IsNullOrEmpty(request.CurrentPassword)
            && !string.IsNullOrEmpty(request.NewPassword)
            && string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            AddError(errors, nameof(request.NewPassword), "Новый пароль должен отличаться от текущего.");
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(ToDictionary(errors));
        }

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

        var result = await userManager
            .ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var (field, message) = MapIdentityError(error);
                AddError(errors, field, message);
            }

            return TypedResults.ValidationProblem(ToDictionary(errors));
        }

        // MLC-059 — успешная смена снимает требование форс-смены (вход по временному паролю
        // после создания/сброса учётки). Тот же эндпоинт обслуживает и обычную смену с
        // /profile, и блокирующий экран форс-смены.
        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await userManager.UpdateAsync(user).ConfigureAwait(false);
        }

        await signInManager.RefreshSignInAsync(user).ConfigureAwait(false);

        await audit.LogAsync(
            AuditActionType.AdminPasswordChanged,
            initiator: user.UserName!,
            description: $"Администратор {user.UserName} сменил пароль.",
            ct: ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static (string Field, string Message) MapIdentityError(IdentityError error) => error.Code switch
    {
        "PasswordMismatch" => (nameof(ChangePasswordRequest.CurrentPassword), "Неверный текущий пароль."),
        "PasswordTooShort" => (nameof(ChangePasswordRequest.NewPassword), "Новый пароль должен быть не короче 12 символов."),
        "PasswordRequiresDigit" => (nameof(ChangePasswordRequest.NewPassword), "Новый пароль должен содержать хотя бы одну цифру."),
        "PasswordRequiresLower" => (nameof(ChangePasswordRequest.NewPassword), "Новый пароль должен содержать строчную букву."),
        "PasswordRequiresUpper" => (nameof(ChangePasswordRequest.NewPassword), "Новый пароль должен содержать заглавную букву."),
        "PasswordRequiresNonAlphanumeric" => (nameof(ChangePasswordRequest.NewPassword), "Новый пароль должен содержать хотя бы один спецсимвол."),
        "PasswordRequiresUniqueChars" => (nameof(ChangePasswordRequest.NewPassword), "Новый пароль должен содержать больше уникальных символов."),
        _ => (nameof(ChangePasswordRequest.NewPassword), error.Description),
    };

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list))
        {
            list = [];
            errors[field] = list;
        }

        list.Add(message);
    }

    private static Dictionary<string, string[]> ToDictionary(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.Ordinal);
}
