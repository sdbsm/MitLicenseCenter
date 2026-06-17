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

// MLC-058 — управление учётками пользователей панели из UI вместо консольной утилиты
// reset-admin (раздел переименован «Администраторы»→«Пользователи» в MLC-060). Работаем
// поверх готовой Identity через UserManager<AppUser> (а не голый DbContext — пользователи
// идут через Identity); схема не меняется → миграции нет.
// «Отключение» = Identity-lockout (LockoutEnd = MaxValue), «включение» = снятие lockout.
// ADR-20: Identity напрямую в Web допустим; генерация пароля — через Application-порт.
public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/users")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Users");

        group.MapGet("/", ListAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("/", CreateAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/reset-password", ResetPasswordAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/disable", DisableAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/enable", EnableAsync).RequireAuthorization(Roles.Admin);
        group.MapPost("/{id:guid}/role", ChangeRoleAsync).RequireAuthorization(Roles.Admin);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(Roles.Admin);
    }

    internal static async Task<Ok<UserListResponse>> ListAsync(
        UserManager<AppUser> userManager,
        TimeProvider clock,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var users = await userManager.Users
            .OrderBy(u => u.UserName)
            .ToListAsync(ct).ConfigureAwait(false);

        var items = new List<UserResponse>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
            items.Add(new UserResponse(user.Id, user.UserName!, roles.ToArray(), IsActive(user, now), user.LastLoginAt));
        }

        return TypedResults.Ok(new UserListResponse(items));
    }

    internal static async Task<Results<Created<UserCreatedResponse>, ValidationProblem, Conflict<ProblemDetails>>> CreateAsync(
        [FromBody] CreateUserRequest request,
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
            errors[nameof(CreateUserRequest.UserName)] = ["Логин не может быть пустым."];
        }
        if (!Roles.All.Contains(role, StringComparer.Ordinal))
        {
            errors[nameof(CreateUserRequest.Role)] = ["Недопустимая роль. Допустимы: Admin, Viewer."];
        }
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // Happy-path pre-check; backstop — нормализованный unique-индекс Identity ниже
        // (CreateAsync вернёт DuplicateUserName на гонке).
        if (await userManager.FindByNameAsync(userName).ConfigureAwait(false) is not null)
        {
            return TypedResults.Conflict(Problems.UserUsernameDuplicate(userName));
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
                return TypedResults.Conflict(Problems.UserUsernameDuplicate(userName));
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

        await httpContext.AuditAsync(audit, AuditActionType.UserCreated,
            init => AuditDescriptions.UserCreated(userName, role, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.Created(
            $"/api/v1/users/{user.Id}",
            new UserCreatedResponse(user.Id, userName, password));
    }

    internal static async Task<Results<Ok<UserPasswordResetResponse>, NotFound<ProblemDetails>>> ResetPasswordAsync(
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
            return TypedResults.NotFound(Problems.UserNotFound());
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

        await httpContext.AuditAsync(audit, AuditActionType.UserPasswordReset,
            init => AuditDescriptions.UserPasswordReset(user.UserName!, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.Ok(new UserPasswordResetResponse(password));
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
            return TypedResults.NotFound(Problems.UserNotFound());
        }

        // Guard «сам себя» — нельзя отключить собственную учётку.
        var initiatorName = httpContext.ResolveInitiator();
        var current = await userManager.FindByNameAsync(initiatorName).ConfigureAwait(false);
        if (current is not null && current.Id == user.Id)
        {
            return TypedResults.Conflict(Problems.UserCannotDisableSelf());
        }

        // Guard «последний активный администратор» — считаем именно учётки роли Admin
        // (не любые активные): с одним Viewer панель станет неуправляемой. Применяем,
        // только если отключаемая учётка сама в роли Admin.
        if (await userManager.IsInRoleAsync(user, Roles.Admin).ConfigureAwait(false)
            && !await HasOtherActiveAdminAsync(userManager, user.Id, clock.GetUtcNow()).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.UserLastActiveAdmin());
        }

        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue).ConfigureAwait(false);

        // MLC-109 (SEC-01) — ротируем security-stamp, чтобы активная кука жертвы умерла в
        // пределах интервала ревалидации (см. SecurityStampValidator в Program.cs). Сам по
        // себе lockout закрывает только будущий вход (PasswordSignInAsync), уже выданную
        // sliding-куку он не отзывает.
        await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.UserDisabled,
            init => AuditDescriptions.UserDisabled(user.UserName!, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    // MLC-180 — жёсткое удаление учётки (UserManager.DeleteAsync). Те же guard'ы, что у
    // отключения (self / последний активный Admin) применяются ДО удаления; аудит пишется
    // ТОЛЬКО после успешного удаления (не пишем запись-ложь на отказе). «Отключить» остаётся.
    internal static async Task<Results<NoContent, NotFound<ProblemDetails>, Conflict<ProblemDetails>>> DeleteAsync(
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
            return TypedResults.NotFound(Problems.UserNotFound());
        }

        // Guard «сам себя» — нельзя удалить собственную учётку (как у отключения).
        var initiatorName = httpContext.ResolveInitiator();
        var current = await userManager.FindByNameAsync(initiatorName).ConfigureAwait(false);
        if (current is not null && current.Id == user.Id)
        {
            return TypedResults.Conflict(Problems.UserCannotDisableSelf());
        }

        // Guard «последний активный администратор» — как у отключения: считаем именно учётки
        // роли Admin; применяем, только если удаляемая учётка сама в роли Admin.
        if (await userManager.IsInRoleAsync(user, Roles.Admin).ConfigureAwait(false)
            && !await HasOtherActiveAdminAsync(userManager, user.Id, clock.GetUtcNow()).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.UserLastActiveAdmin());
        }

        // Снимаем имя ДО удаления — после DeleteAsync сущность отсоединена.
        var userName = user.UserName!;

        var result = await userManager.DeleteAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            // Guard'ы уже пройдены — провал удаления не ожидается; не маскируем его
            // 409/400 (как CreateAsync с прочими сбоями), отдаём как 500.
            throw new InvalidOperationException(
                "Не удалось удалить учётную запись: "
                + string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        await httpContext.AuditAsync(audit, AuditActionType.UserDeleted,
            init => AuditDescriptions.UserDeleted(userName, init),
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
            return TypedResults.NotFound(Problems.UserNotFound());
        }

        // Снятие lockout + сброс счётчика неудачных входов (как reset-admin --unlock).
        await userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.UserEnabled,
            init => AuditDescriptions.UserEnabled(user.UserName!, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    internal static async Task<Results<Ok, ValidationProblem, NotFound<ProblemDetails>, Conflict<ProblemDetails>>> ChangeRoleAsync(
        Guid id,
        [FromBody] ChangeUserRoleRequest request,
        UserManager<AppUser> userManager,
        IAuditLogger audit,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        var role = (request.Role ?? string.Empty).Trim();
        if (!Roles.All.Contains(role, StringComparer.Ordinal))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [nameof(ChangeUserRoleRequest.Role)] = ["Недопустимая роль. Допустимы: Admin, Viewer."],
            });
        }

        var user = await userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.NotFound(Problems.UserNotFound());
        }

        // Guard «сам себе» — смена роли собственной учётке = потеря доступа при само-разжаловании.
        var initiatorName = httpContext.ResolveInitiator();
        var current = await userManager.FindByNameAsync(initiatorName).ConfigureAwait(false);
        if (current is not null && current.Id == user.Id)
        {
            return TypedResults.Conflict(Problems.UserCannotChangeOwnRole());
        }

        var currentRoles = await userManager.GetRolesAsync(user).ConfigureAwait(false);

        // Идемпотентность: учётка уже ровно в целевой роли → no-op, без аудита.
        if (currentRoles.Count == 1 && string.Equals(currentRoles[0], role, StringComparison.Ordinal))
        {
            return TypedResults.Ok();
        }

        // Guard «последний активный администратор»: разжалование Admin→не-Admin, когда
        // других активных Admin нет, оставит панель неуправляемой. Переиспользуем ту же
        // проверку, что и отключение учётки.
        var demotingFromAdmin = currentRoles.Contains(Roles.Admin, StringComparer.Ordinal)
            && !string.Equals(role, Roles.Admin, StringComparison.Ordinal);
        if (demotingFromAdmin
            && !await HasOtherActiveAdminAsync(userManager, user.Id, clock.GetUtcNow()).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.UserLastActiveAdmin());
        }

        var oldRole = currentRoles.Count > 0 ? string.Join(", ", currentRoles) : "—";
        if (currentRoles.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles).ConfigureAwait(false);
            if (!removeResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Не удалось снять текущие роли: "
                    + string.Join("; ", removeResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
            }
        }

        var addResult = await userManager.AddToRoleAsync(user, role).ConfigureAwait(false);
        if (!addResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Не удалось назначить роль '{role}': "
                + string.Join("; ", addResult.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        // MLC-109 (SEC-01) — ротируем security-stamp: смена роли должна отозвать старую куку
        // жертвы с прежними role-claims в пределах интервала ревалидации (см. Program.cs).
        // Add/RemoveFromRole stamp сами НЕ трогают, поэтому без этого вызова разжалованный
        // Admin продолжал бы ходить с Admin-claims в куке до её естественного истечения (8h).
        await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        await httpContext.AuditAsync(audit, AuditActionType.UserRoleChanged,
            init => AuditDescriptions.UserRoleChanged(user.UserName!, oldRole, role, init),
            tenantId: null, ct).ConfigureAwait(false);

        return TypedResults.Ok();
    }

    // Есть ли среди учёток роли Admin хотя бы один активный, отличный от `excludeUserId`.
    // Общий guard для отключения (MLC-058) и разжалования роли (MLC-061): счёт идёт
    // именно по роли Admin, а не по любым активным учёткам.
    private static async Task<bool> HasOtherActiveAdminAsync(
        UserManager<AppUser> userManager, Guid excludeUserId, DateTimeOffset now)
    {
        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin).ConfigureAwait(false);
        return admins.Any(a => a.Id != excludeUserId && IsActive(a, now));
    }

    // «Активна» = не под действующим lockout. Identity-lockout с LockoutEnd в будущем
    // (у нас — DateTimeOffset.MaxValue) блокирует вход в PasswordSignInAsync.
    private static bool IsActive(AppUser user, DateTimeOffset now) =>
        user.LockoutEnd is not { } end || end <= now;
}
