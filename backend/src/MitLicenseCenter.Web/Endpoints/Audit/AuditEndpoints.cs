using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

public static class AuditEndpoints
{
    private const int DefaultPageSize = 50;
    // Server-side clamp: страница тяжелее tenants/infobases — фиксируем шаги вручную.
    private static readonly int[] AllowedPageSizes = [25, 50, 100];
    // Свободный текст фильтра search ограничен по длине — защита от
    // непомерных LIKE-термов; превышение → ValidationProblem, не 500.
    private const int MaxFreeTextLength = 200;

    public static void MapAuditEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/audit")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Audit");

        group.MapGet("/", ListAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/retention", GetRetentionAsync).RequireAuthorization(Roles.Viewer);
    }

    // PR 4.3: узкий Viewer-readable endpoint для banner на /audit. НЕ расширяем
    // GET /settings до Viewer — там 14 ключей включая описания cluster creds.
    internal static Ok<AuditRetentionResponse> GetRetentionAsync(ISettingsSnapshot settings)
    {
        var days = settings.GetInt(SettingKey.AuditRetentionDays) ?? 365;
        return TypedResults.Ok(new AuditRetentionResponse(days));
    }

    internal static async Task<Results<Ok<AuditPagedResponse>, ValidationProblem>> ListAsync(
        AppDbContext db,
        [FromQuery] string? actionType,
        [FromQuery] Guid? tenantId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        AuditActionType? parsedActionType = null;
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            if (Enum.TryParse<AuditActionType>(actionType, ignoreCase: true, out var parsed)
                && Enum.IsDefined(parsed))
            {
                parsedActionType = parsed;
            }
            else
            {
                errors[nameof(actionType)] = ["Неизвестный тип действия."];
            }
        }

        if (from is { } f && to is { } t && t < f)
        {
            errors[nameof(to)] = ["Конец диапазона раньше начала."];
        }

        var searchTerm = search?.Trim();
        if (searchTerm is { Length: > MaxFreeTextLength })
        {
            errors[nameof(search)] = [$"Не длиннее {MaxFreeTextLength} символов."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var p = page is > 0 ? page.Value : 1;
        var ps = pageSize is { } requested && AllowedPageSizes.Contains(requested)
            ? requested
            : DefaultPageSize;

        var query = db.AuditLogs.AsNoTracking();
        if (parsedActionType is { } action)
        {
            query = query.Where(x => x.ActionType == action);
        }
        if (tenantId is { } tid)
        {
            query = query.Where(x => x.TenantId == tid);
        }
        if (from is { } fromUtc)
        {
            query = query.Where(x => x.Timestamp >= fromUtc);
        }
        if (to is { } toUtc)
        {
            query = query.Where(x => x.Timestamp <= toUtc);
        }
        if (!string.IsNullOrEmpty(searchTerm))
        {
            // Подстрочный поиск по описанию И инициатору обычным string.Contains →
            // EF Core SQL Server-провайдер транслирует его в `LIKE '%term%'`.
            // Регистронезависимость обеспечивает РЕГИСТРОНЕЗАВИСИМАЯ collation БД
            // (дефолтная *_CI_*), а не код. Перегрузку с StringComparison.OrdinalIgnoreCase
            // использовать НЕЛЬЗЯ: SQL Server-провайдер EF Core её НЕ транслирует и бросает в
            // рантайме (подтверждено доками MS — CA1862 для EF-запросов положено подавлять;
            // OrdinalIgnoreCase-Contains добавлен только в Cosmos-провайдер, не SQL Server).
#pragma warning disable CA1862 // EF-запрос: трансляция в SQL LIKE, регистр — за collation БД
            query = query.Where(x =>
                x.Description.Contains(searchTerm) || x.Initiator.Contains(searchTerm));
#pragma warning restore CA1862
        }

        // CountAsync и Skip/Take по одному и тому же IQueryable — EF проводит их как
        // два SQL-запроса; запускаем последовательно (один scoped DbContext не
        // поддерживает параллельные операции).
        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(x => new AuditEntryResponse(
                x.Id,
                x.Timestamp,
                x.ActionType,
                x.Reason,
                x.Initiator,
                x.Description,
                x.TenantId))
            .ToListAsync(ct).ConfigureAwait(false);

        return TypedResults.Ok(new AuditPagedResponse(items, total, p, ps));
    }
}
