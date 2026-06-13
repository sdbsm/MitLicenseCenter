using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Web.Endpoints;

public static class TenantsEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public static void MapTenantsEndpoints(this IEndpointRouteBuilder endpoints, ApiVersionSet versionSet)
    {
        var group = endpoints
            .MapGroup("/api/v{version:apiVersion}/tenants")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0))
            .WithTags("Tenants");

        group.MapGet("/", ListAsync).RequireAuthorization(Roles.Viewer);
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization(Roles.Viewer);
        group.MapPost("/", CreateAsync).RequireAuthorization(Roles.Admin);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(Roles.Admin);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(Roles.Admin);
    }

    internal static async Task<Ok<TenantListResponse>> ListAsync(
        AppDbContext db,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var p = page is > 0 ? page.Value : 1;
        var ps = pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;

        var query = db.Tenants.AsNoTracking().OrderBy(t => t.Name);
        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(t => new TenantResponse(
                t.Id,
                t.Name,
                t.MaxConcurrentLicenses,
                t.IsActive,
                t.CreatedAt,
                t.UpdatedAt,
                db.Infobases.Count(x => x.TenantId == t.Id)))
            .ToListAsync(ct).ConfigureAwait(false);

        return TypedResults.Ok(new TenantListResponse(items, total, p, ps));
    }

    private static async Task<Results<Ok<TenantResponse>, NotFound>> GetAsync(
        Guid id,
        AppDbContext db,
        CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        return tenant is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(tenant.ToResponse());
    }

    internal static async Task<Results<Created<TenantResponse>, ValidationProblem, Conflict<ProblemDetails>>> CreateAsync(
        [FromBody] CreateTenantRequest request,
        AppDbContext db,
        IAuditLogger audit,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        var normalized = (request.Name ?? string.Empty).Trim();
        var errors = ValidateTenant(normalized, request.MaxConcurrentLicenses);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (await db.Tenants.AnyAsync(t => t.Name == normalized, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.TenantNameDuplicate(normalized));
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = normalized,
            MaxConcurrentLicenses = request.MaxConcurrentLicenses,
            IsActive = request.IsActive,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };

        db.Tenants.Add(tenant);
        // MLC-004 — предварительный AnyAsync выше остаётся happy-path'ом; на гонке двух
        // вставок backstop — уникальный индекс IX_Tenants_Name. DbUpdateException мапим
        // в тот же 409, что и happy-path, вместо голого 500.
        var conflict = await db.SaveWithUniquenessBackstopAsync(ct,
            (UniqueIndexViolation.TenantName, () => Problems.TenantNameDuplicate(normalized))).ConfigureAwait(false);
        if (conflict is not null)
        {
            return TypedResults.Conflict(conflict);
        }

        await httpContext.AuditAsync(audit, AuditActionType.TenantCreated,
            init => AuditDescriptions.TenantCreated(tenant.Name, init),
            tenant.Id, ct).ConfigureAwait(false);

        return TypedResults.Created($"/api/v1/tenants/{tenant.Id}", tenant.ToResponse());
    }

    internal static async Task<Results<Ok<TenantResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> UpdateAsync(
        Guid id,
        [FromBody] UpdateTenantRequest request,
        AppDbContext db,
        IAuditLogger audit,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        var normalized = (request.Name ?? string.Empty).Trim();
        var errors = ValidateTenant(normalized, request.MaxConcurrentLicenses);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!string.Equals(tenant.Name, normalized, StringComparison.Ordinal)
            && await db.Tenants.AnyAsync(t => t.Name == normalized && t.Id != id, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.TenantNameDuplicate(normalized));
        }

        // MLC-119 (BE-11) — фиксируем старый лимит ДО присвоения нового, чтобы при
        // фактическом изменении дописать структурированное событие LimitChanged.
        var oldLimit = tenant.MaxConcurrentLicenses;

        tenant.Name = normalized;
        tenant.MaxConcurrentLicenses = request.MaxConcurrentLicenses;
        tenant.IsActive = request.IsActive;
        tenant.UpdatedAt = clock.GetUtcNow().UtcDateTime;

        // MLC-004 — backstop на гонке (см. CreateAsync): нарушение IX_Tenants_Name мапим
        // в тот же 409, что и предварительный AnyAsync.
        var conflict = await db.SaveWithUniquenessBackstopAsync(ct,
            (UniqueIndexViolation.TenantName, () => Problems.TenantNameDuplicate(normalized))).ConfigureAwait(false);
        if (conflict is not null)
        {
            return TypedResults.Conflict(conflict);
        }

        await httpContext.AuditAsync(audit, AuditActionType.TenantUpdated,
            init => AuditDescriptions.TenantUpdated(tenant.Name, init),
            tenant.Id, ct).ConfigureAwait(false);

        // MLC-119 (BE-11) — смена лимита лицензий чувствительна (прямо влияет на enforcement)
        // и неотличима от переименования в общем TenantUpdated. При фактическом изменении
        // дописываем ДОПОЛНИТЕЛЬНОЕ структурированное событие LimitChanged со старым→новым
        // значением (action-first, как TenantUpdated; PUT, меняющий и имя, и лимит, пишет обе).
        if (oldLimit != tenant.MaxConcurrentLicenses)
        {
            await httpContext.AuditAsync(audit, AuditActionType.LimitChanged,
                init => AuditDescriptions.LimitChanged(tenant.Name, oldLimit, tenant.MaxConcurrentLicenses, init),
                tenant.Id, ct).ConfigureAwait(false);
        }

        return TypedResults.Ok(tenant.ToResponse());
    }

    internal static async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>>> DeleteAsync(
        Guid id,
        AppDbContext db,
        IAuditLogger audit,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (tenant is null)
        {
            return TypedResults.NotFound();
        }

        if (await db.Infobases.AnyAsync(x => x.TenantId == id, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.TenantHasInfobases());
        }

        var name = tenant.Name;
        // MLC-119 (BE-01) — удаление и аудит коммитятся ОДНИМ SaveChanges (атомарно: оба
        // или ничего). Запись аудита enlist'им (без своего SaveChanges), чтобы при сбое
        // удаления не оставалась ложная TenantDeleted. FK SetNull в той же транзакции
        // обнулит TenantId в новой строке после удаления tenant — это корректно: запись
        // остаётся, ссылка обнуляется.
        db.Tenants.Remove(tenant);
        httpContext.EnlistAudit(audit, AuditActionType.TenantDeleted,
            init => AuditDescriptions.TenantDeleted(name, init),
            id);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    // Ручная проверка (DataAnnotations [Required]/[Range] на контрактах в minimal API в
    // runtime НЕ прогоняются — они только документируют Swagger). [Range(0,100_000)] на
    // MaxConcurrentLicenses без рантайм-проверки молча сохранял бы ≤0 и отключал контроль
    // лимитов клиента (ReconciliationJob пропускает клиента при лимите ≤0) — BE-03.
    private const int MaxConcurrentLicensesLimit = 100_000;

    private static Dictionary<string, string[]> ValidateTenant(string normalized, int maxConcurrentLicenses)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            errors[nameof(CreateTenantRequest.Name)] = ["Название клиента не может быть пустым."];
        }
        if (maxConcurrentLicenses < 0 || maxConcurrentLicenses > MaxConcurrentLicensesLimit)
        {
            errors[nameof(CreateTenantRequest.MaxConcurrentLicenses)] =
                [$"Лимит лицензий должен быть в диапазоне от 0 до {MaxConcurrentLicensesLimit}."];
        }
        return errors;
    }
}
