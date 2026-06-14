using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-136 (R12c) — оптимистическая блокировка Tenant через rowversion-токен.
// EF InMemory НЕ генерирует rowversion и не воспроизводит конкурентность (как unique-индексы,
// MLC-008), поэтому реальный конфликт эмулируем перехватчиком, бросающим
// DbUpdateConcurrencyException на SaveChanges — ровно так, как это сделал бы SQL Server при
// UPDATE ... WHERE RowVersion = @original, затронувшем 0 строк. UpdateAsync с непустым
// request.RowVersion должен поймать его и вернуть 409 TENANT_CONCURRENCY_CONFLICT, не уходя 500.
public sealed class TenantConcurrencyTests
{
    private static readonly TimeProvider Clock =
        TestHelpers.FixedClock(new DateTime(2026, 6, 14, 12, 0, 0, DateTimeKind.Utc));

    private static Tenant SeededTenant(string name = "Acme", int limit = 10) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxConcurrentLicenses = limit,
        IsActive = true,
        CreatedAt = Clock.GetUtcNow().UtcDateTime,
    };

    [Fact]
    public async Task Update_with_stale_rowversion_maps_to_409_TENANT_CONCURRENCY_CONFLICT()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            new DbUpdateConcurrencyException("Database operation expected to affect 1 row(s) but actually affected 0 row(s)."));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var tenant = SeededTenant();
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        interceptor.Armed = true;
        // Непустой rowVersion (base64-декодированный байтовый массив) — клиент прислал
        // прочитанную версию; перехватчик имитирует, что строку успели изменить.
        var result = await TenantsEndpoints.UpdateAsync(
            tenant.Id,
            new UpdateTenantRequest("Acme Renamed", 25, IsActive: true, RowVersion: [1, 2, 3, 4, 5, 6, 7, 8]),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("admin"),
            Clock,
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.TenantConcurrencyConflict);
    }

    [Fact]
    public async Task Update_without_rowversion_succeeds_backward_compatible()
    {
        // Обратная совместимость: rowVersion=null (старый клиент / InMemory без токена) —
        // апдейт проходит как раньше, OriginalValue не выставляется, конфликта нет.
        await using var db = TestHelpers.NewInMemoryDb();
        var tenant = SeededTenant(limit: 10);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var result = await TenantsEndpoints.UpdateAsync(
            tenant.Id,
            new UpdateTenantRequest("Acme Renamed", 20, IsActive: true),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext("admin"),
            Clock,
            CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<TenantResponse>>().Subject;
        ok.Value!.Name.Should().Be("Acme Renamed");
        ok.Value.MaxConcurrentLicenses.Should().Be(20);
        // Контракт ответа несёт поле RowVersion (под InMemory оно null — токен не материализуется).
        ok.Value.RowVersion.Should().BeNull();
    }
}
