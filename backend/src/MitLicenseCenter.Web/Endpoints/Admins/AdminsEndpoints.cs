using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Identity;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Endpoints;

// MLC-058 — управление учётками администраторов панели из UI вместо консольной утилиты
// reset-admin. Работаем поверх готовой Identity через UserManager<AppUser> (а не голый
// DbContext — пользователи идут через Identity); схема не меняется → миграции нет.
// «Отключение» = Identity-lockout (LockoutEnd = MaxValue), «включение» = снятие lockout.
// ADR-20: Identity напрямую в Web допустим; генерация пароля — через Application-порт.
public static class AdminsEndpoints
{
    public static void MapAdminsEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/admins")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Admins");

        group.MapGet("/", ListAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("/", CreateAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/reset-password", ResetPasswordAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/disable", DisableAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/enable", EnableAsync).RequireAuthorization(Roles.Admin);
    }

    internal static async Task<Ok<AdminListResponse>> ListAsync(
        UserManager<AppUser> userManager,
        TimeProvider clock,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var users = await userManager.Users
            .OrderBy(u => u.UserName)
            .ToListAsync(ct).ConfigureAwait(false);

        var items = new List<AdminResponse>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
            items.Add(new AdminResponse(user.Id, user.UserName!, roles.ToArray(), IsActive(user, now), user.LastLoginAt));
        }

        return TypedResults.Ok(new AdminListResponse(items));
    }

    internal static async Task<Results<Created<AdminCreatedResponse>, ValidationProblem, Conflict<ProblemDetails>>> CreateAsync(
        [FromBody] CreateAdminRequest request,
        UserManager<AppUser> userManager,
        IInitialPasswordGenerator passwordGenerator,
        IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var userName = (request.UserName ?? string.Empty).Trim();
        var role = (request.Role ?? string.Empty).Trim();

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(userName))
        {
            errors[nameof(CreateAdminRequest.UserName)] = ["Логин не может быть пустым."];
        }
        if (!Roles.All.Contains(role, StringComparer.Ordinal))
        {
            errors[nameof(CreateAdminRequest.Role)] = ["Недопустимая роль. Допустимы: Admin, Viewer."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // Happy-path pre-check; backstop — нормализованный unique-индекс Identity ниже
        // (CreateAsync вернёт DuplicateUserName на гонке).
        if (await userManager.FindByNameAsync(userName).ConfigureAwait(false) is not null)
        {
            return TypedResults.Conflict(Problems.AdminUsernameDuplicate(userName));
        }

        var password = passwordGenerator.Generate();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = null,
            EmailConfirmed = true,
            // MLC-059 — выданный пароль временный: при первом входе пользователя обяжем
            // сменить его (админ не знает чужой постоянный пароль).
            MustChangePassword = true,
        };

        var createResult = await userManager.CreateAsync(user, password).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(e => e.Code == "DuplicateUserName"))
            {
                return TypedResults.Conflict(Problems.AdminUsernameDuplicate(userName));
            }

            // Сгенерированный пароль удовлетворяет политике, а роль уже провалидирована —
            // прочие сбои не ожидаются; не маскируем их 409/400, отдаём как 500.
            throw new InvalidOperationException(
                "Не удалось создать учётную запись: "
                + string.Join("; ", createResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        var assignResult = await userManager.AddToRoleAsync(user, role).ConfigureAwait(false);
        if (!assignResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Не удалось назначить роль '{role}': "
                + string.Join("; ", assignResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        await httpContext.AuditAsync(audit, AuditActionType.AdminCreated,
            init => AuditDescriptions.AdminCreated(userName, role, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.Created(
            $"/api/v1/admins/{user.Id}",
            new AdminCreatedResponse(user.Id, userName, password));
    }

    internal static async Task<Results<Ok<AdminPasswordResetResponse>, NotFound<ProblemDetails>>> ResetPasswordAsync(
        Guid id,
        UserManager<AppUser> userManager,
        IInitialPasswordGenerator passwordGenerator,
        IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.NotFound(Problems.AdminNotFound());
        }

        // Сброс через штатный поток Identity (корректный хеш + проверка политики), как в
        // reset-admin: GeneratePasswordResetTokenAsync → ResetPasswordAsync.
        var password = passwordGenerator.Generate();
        var token = await userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
        var resetResult = await userManager.ResetPasswordAsync(user, token, password).ConfigureAwait(false);
        if (!resetResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Не удалось сбросить пароль: "
                + string.Join("; ", resetResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        // MLC-059 — сброшенный пароль временный: обяжем пользователя сменить его при
        // следующем входе.
        user.MustChangePassword = true;
        await userManager.UpdateAsync(user).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.AdminPasswordReset,
            init => AuditDescriptions.AdminPasswordReset(user.UserName!, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.Ok(new AdminPasswordResetResponse(password));
    }

    internal static async Task<Results<NoContent, NotFound<ProblemDetails>, Conflict<ProblemDetails>>> DisableAsync(
        Guid id,
        UserManager<AppUser> userManager,
        IAuditLogger audit,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.NotFound(Problems.AdminNotFound());
        }

        // Guard «сам себя» — нельзя отключить собственную учётку.
        var initiatorName = httpContext.ResolveInitiator();
        var current = await userManager.FindByNameAsync(initiatorName).ConfigureAwait(false);
        if (current is not null && current.Id == user.Id)
        {
            return TypedResults.Conflict(Problems.AdminCannotDisableSelf());
        }

        // Guard «последний активный администратор» — считаем именно учётки роли Admin
        // (не любые активные): с одним Viewer панель станет неуправляемой. Применяем,
        // только если отключаемая учётка сама в роли Admin.
        if (await userManager.IsInRoleAsync(user, Roles.Admin).ConfigureAwait(false))
        {
            var now = clock.GetUtcNow();
            var admins = await userManager.GetUsersInRoleAsync(Roles.Admin).ConfigureAwait(false);
            var otherActiveAdmin = admins.Any(a => a.Id != user.Id && IsActive(a, now));
            if (!otherActiveAdmin)
            {
                return TypedResults.Conflict(Problems.AdminLastActive());
            }
        }

        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.AdminDisabled,
            init => AuditDescriptions.AdminDisabled(user.UserName!, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    internal static async Task<Results<NoContent, NotFound<ProblemDetails>>> EnableAsync(
        Guid id,
        UserManager<AppUser> userManager,
        IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.NotFound(Problems.AdminNotFound());
        }

        // Снятие lockout + сброс счётчика неудачных входов (как reset-admin --unlock).
        await userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.AdminEnabled,
            init => AuditDescriptions.AdminEnabled(user.UserName!, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    // «Активна» = не под действующим lockout. Identity-lockout с LockoutEnd в будущем
    // (у нас — DateTimeOffset.MaxValue) блокирует вход в PasswordSignInAsync.
    private static bool IsActive(AppUser user, DateTimeOffset now) =>
        user.LockoutEnd is not { } end || end <= now;
}
