using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-130 (UX-05): подстрочный поиск клиентов на /tenants.
public sealed class TenantsSearchTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Search_filters_by_name_substring_same_case()
    {
        // InMemory-тест: проверяем совпадение того же регистра.
        // Кросс-регистр гарантирует CI-collation SQL Server — InMemory его не эмулирует.
        using var db = TestHelpers.NewInMemoryDb();
        db.Tenants.AddRange(
            MakeTenant("Acme Corp"),
            MakeTenant("Globex Industries"),
            MakeTenant("Acme Holdings"));
        await db.SaveChangesAsync();

        var result = await TenantsEndpoints.ListAsync(
            db,
            page: 1,
            pageSize: 50,
            search: "Acme",
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<TenantListResponse>>().Subject;
        ok.Value!.Total.Should().Be(2, "поиск учитывает фильтр в Total");
        ok.Value.Items.Select(t => t.Name).Should().BeEquivalentTo(["Acme Corp", "Acme Holdings"]);
    }

    [Fact]
    public async Task Search_empty_string_returns_all()
    {
        using var db = TestHelpers.NewInMemoryDb();
        db.Tenants.AddRange(
            MakeTenant("Alpha"),
            MakeTenant("Beta"),
            MakeTenant("Gamma"));
        await db.SaveChangesAsync();

        var result = await TenantsEndpoints.ListAsync(
            db,
            page: 1,
            pageSize: 50,
            search: "",
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<TenantListResponse>>().Subject;
        ok.Value!.Total.Should().Be(3, "пустой поиск — без фильтрации");
    }

    [Fact]
    public async Task Search_null_returns_all()
    {
        using var db = TestHelpers.NewInMemoryDb();
        db.Tenants.AddRange(MakeTenant("Alpha"), MakeTenant("Beta"));
        await db.SaveChangesAsync();

        var result = await TenantsEndpoints.ListAsync(
            db,
            page: 1,
            pageSize: 50,
            search: null,
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<TenantListResponse>>().Subject;
        ok.Value!.Total.Should().Be(2);
    }

    [Fact]
    public async Task Search_too_long_returns_validation_problem()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var result = await TenantsEndpoints.ListAsync(
            db,
            page: 1,
            pageSize: 50,
            search: new string('x', 201),
            ct: CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>(
            "строка поиска длиннее 200 символов → ValidationProblem");
    }

    [Fact]
    public async Task Search_with_no_match_returns_empty_page_total_zero()
    {
        using var db = TestHelpers.NewInMemoryDb();
        db.Tenants.Add(MakeTenant("Acme Corp"));
        await db.SaveChangesAsync();

        var result = await TenantsEndpoints.ListAsync(
            db,
            page: 1,
            pageSize: 50,
            search: "Globex",
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<TenantListResponse>>().Subject;
        ok.Value!.Total.Should().Be(0);
        ok.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_resets_count_for_pagination()
    {
        // Total учитывает фильтр → правильная пагинация при поиске.
        using var db = TestHelpers.NewInMemoryDb();
        for (var i = 0; i < 10; i++) db.Tenants.Add(MakeTenant($"Acme {i}"));
        for (var i = 0; i < 5; i++) db.Tenants.Add(MakeTenant($"Globex {i}"));
        await db.SaveChangesAsync();

        var result = await TenantsEndpoints.ListAsync(
            db,
            page: 1,
            pageSize: 50,
            search: "Acme",
            ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<Ok<TenantListResponse>>().Subject;
        ok.Value!.Total.Should().Be(10, "Total = число записей, попавших под фильтр");
    }

    private static Tenant MakeTenant(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxConcurrentLicenses = 10,
        IsActive = true,
        CreatedAt = Now,
    };
}
