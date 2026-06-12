using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// BE-03 (MLC-114) — runtime-проверка диапазона MaxConcurrentLicenses. DataAnnotations
// [Range(0,100_000)] на контракте в minimal API в runtime НЕ прогоняются (только Swagger),
// поэтому ≤0/>лимита раньше молча сохранялись и отключали контроль лимитов клиента.
// Проверяем именно ручной путь хендлеров CreateAsync/UpdateAsync → 400 ValidationProblem.
public sealed class TenantsLimitRangeTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(100_001)]
    public async Task Create_with_out_of_range_limit_returns_validation_problem(int limit)
    {
        await using var db = TestHelpers.NewInMemoryDb();

        var result = await TenantsEndpoints.CreateAsync(
            new CreateTenantRequest("Acme", limit, IsActive: true),
            db, new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext("admin"),
            TimeProvider.System, CancellationToken.None);

        var problem = result.Result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey(nameof(CreateTenantRequest.MaxConcurrentLicenses));
        db.Tenants.Should().BeEmpty("клиент с недопустимым лимитом не сохраняется");
    }

    [Fact]
    public async Task Create_with_in_range_limit_succeeds()
    {
        await using var db = TestHelpers.NewInMemoryDb();

        var result = await TenantsEndpoints.CreateAsync(
            new CreateTenantRequest("Acme", 0, IsActive: true),
            db, new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext("admin"),
            TimeProvider.System, CancellationToken.None);

        result.Result.Should().BeOfType<Created<TenantResponse>>();
    }

    [Fact]
    public async Task Update_with_negative_limit_returns_validation_problem_and_keeps_old_value()
    {
        await using var db = TestHelpers.NewInMemoryDb();
        var id = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = id,
            Name = "Acme",
            MaxConcurrentLicenses = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await TenantsEndpoints.UpdateAsync(
            id, new UpdateTenantRequest("Acme", -5, IsActive: true),
            db, new TestHelpers.CapturingAuditLogger(), TestHelpers.NewHttpContext("admin"),
            TimeProvider.System, CancellationToken.None);

        result.Result.Should().BeOfType<ValidationProblem>();
        db.Tenants.Single().MaxConcurrentLicenses.Should().Be(10, "недопустимый апдейт не применяется");
    }
}
