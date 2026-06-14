using System.ComponentModel.DataAnnotations;
using MitLicenseCenter.Domain.Tenants;

namespace MitLicenseCenter.Web.Endpoints;

public sealed record TenantResponse(
    Guid Id,
    string Name,
    int MaxConcurrentLicenses,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int InfobaseCount = 0,
    // MLC-136 (R12c) — токен оптимистической блокировки. System.Text.Json сериализует
    // byte[] как base64-строку; при null поле опускается (WhenWritingNull). Под EF
    // InMemory токен не материализуется (null → поле отсутствует), что согласуется с
    // omit-null дисциплиной фронта (omittable).
    byte[]? RowVersion = null);

public sealed record TenantListResponse(
    IReadOnlyList<TenantResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record CreateTenantRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: Range(0, 100_000)] int MaxConcurrentLicenses,
    bool IsActive);

public sealed record UpdateTenantRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: Range(0, 100_000)] int MaxConcurrentLicenses,
    bool IsActive,
    // MLC-136 (R12c) — токен оптимистической блокировки, прочитанный клиентом при
    // загрузке формы (base64 → byte[]). ОПЦИОНАЛЕН: null оставляет обратную совместимость
    // (старые клиенты / InMemory-тесты без токена сохраняются как раньше). Непустой токен
    // выставляется как OriginalValue → конкурентный апдейт ловится DbUpdateConcurrencyException.
    byte[]? RowVersion = null);

internal static class TenantMappings
{
    public static TenantResponse ToResponse(this Tenant t) =>
        new(t.Id, t.Name, t.MaxConcurrentLicenses, t.IsActive, t.CreatedAt, t.UpdatedAt,
            RowVersion: t.RowVersion);
}
