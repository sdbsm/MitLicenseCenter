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
        var errors = ValidateName(normalized);
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

        var initiator = httpContext.ResolveInitiator();
        await audit.LogAsync(
            AuditActionType.TenantCreated,
            initiator: initiator,
            description: AuditDescriptions.TenantCreated(tenant.Name, initiator),
            tenantId: tenant.Id,
            ct: ct).ConfigureAwait(false);

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
        var errors = ValidateName(normalized);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (!string.Equals(tenant.Name, normalized, StringComparison.Ordinal)
            && await db.Tenants.AnyAsync(t => t.Name == normalized && t.Id != id, ct).ConfigureAwait(false))
        {
            return TypedResults.Conflict(Problems.TenantNameDuplicate(normalized));
        }

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

        var initiator = httpContext.ResolveInitiator();
        await audit.LogAsync(
            AuditActionType.TenantUpdated,
            initiator: initiator,
            description: AuditDescriptions.TenantUpdated(tenant.Name, initiator),
            tenantId: tenant.Id,
            ct: ct).ConfigureAwait(false);

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
        var initiator = httpContext.ResolveInitiator();
        // Запись аудита кладём ДО удаления, пока tenant ещё существует — FK SetNull
        // потом сам обнулит TenantId в этой строке.
        await audit.LogAsync(
            AuditActionType.TenantDeleted,
            initiator: initiator,
            description: AuditDescriptions.TenantDeleted(name, initiator),
            tenantId: id,
            ct: ct).ConfigureAwait(false);

        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static Dictionary<string, string[]> ValidateName(string normalized)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            errors[nameof(CreateTenantRequest.Name)] = ["Название клиента не может быть пустым."];
        }
        return errors;
    }
}
