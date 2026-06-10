using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-004 — backstop: при гонке двух вставок предварительный AnyAsync проходит у обеих,
// и единственная защита — уникальный индекс БД. Возникающий DbUpdateException должен
// маппиться в тот же задокументированный 409 (ProblemCodes.*), что и happy-path, а не
// уходить голым 500. EF InMemory не воспроизводит unique-violation (MLC-008), поэтому
// гонку эмулируем перехватчиком, бросающим SQL-Server-подобное DbUpdateException.
public sealed class DbUpdateExceptionBackstopTests
{
    // Имитируем сообщение SQL Server о нарушении уникального индекса — имя индекса в нём
    // присутствует дословно, по нему DbUniqueViolation и различает индекс.
    private static DbUpdateException UniqueViolation(string indexName, string table) =>
        new(
            "An error occurred while saving the entity changes.",
            new InvalidOperationException(
                $"Cannot insert duplicate key row in object '{table}' with unique index "
                + $"'{indexName}'. The duplicate key value is (00000000-0000-0000-0000-000000000001)."));

    private static Tenant SeedTenant(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxConcurrentLicenses = 10,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static CreateInfobaseRequest CreateInfobase(Guid tenantId, string name, Guid? clusterId = null) =>
        new(
            TenantId: tenantId,
            Name: name,
            ClusterInfobaseId: clusterId ?? Guid.NewGuid(),
            DatabaseName: "ib",
            Status: InfobaseStatus.Active,
            Publication: new CreatePublicationRequest(
                SiteName: "Default Web Site",
                VirtualPath: "/ib",
                PlatformVersion: "8.3.23.1865",
                PhysicalPathOverride: null));

    private static UpdateInfobaseRequest UpdateInfobase(string name, Guid clusterId) =>
        new(
            Name: name,
            ClusterInfobaseId: clusterId,
            DatabaseName: "ib",
            Status: InfobaseStatus.Active,
            Publication: new UpdatePublicationRequest(
                SiteName: "Default Web Site",
                VirtualPath: "/ib",
                PlatformVersion: "8.3.23.1865",
                PhysicalPathOverride: null));

    // ── DbUniqueViolation.Identify: имя индекса → enum ─────────────────────────────

    // Ожидаемое значение передаём как int (enum internal — не может быть параметром
    // публичного [Theory]-метода), внутри приводим обратно к UniqueIndexViolation.
    [Theory]
    [InlineData("IX_Infobases_ClusterInfobaseId", (int)UniqueIndexViolation.InfobaseClusterId)]
    [InlineData("IX_Infobases_TenantId_Name", (int)UniqueIndexViolation.InfobaseTenantName)]
    [InlineData("IX_Tenants_Name", (int)UniqueIndexViolation.TenantName)]
    public void Identify_maps_known_index_names(string indexName, int expected)
    {
        var ex = UniqueViolation(indexName, "dbo.Test");
        DbUniqueViolation.Identify(ex).Should().Be((UniqueIndexViolation)expected);
    }

    [Fact]
    public void Identify_returns_None_for_unrelated_DbUpdateException()
    {
        var ex = new DbUpdateException("boom", new InvalidOperationException("FK violation REFERENCES some_other_table"));
        DbUniqueViolation.Identify(ex).Should().Be(UniqueIndexViolation.None);
    }

    [Fact]
    public void Identify_returns_None_when_no_inner_exception()
    {
        DbUniqueViolation.Identify(new DbUpdateException("boom")).Should().Be(UniqueIndexViolation.None);
    }

    // ── Endpoint backstop: DbUpdateException → нужный 409 ──────────────────────────

    [Fact]
    public async Task Create_infobase_cluster_id_race_maps_to_409_INFOBASE_ALREADY_ASSIGNED()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            UniqueViolation("IX_Infobases_ClusterInfobaseId", "dbo.Infobases"));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var acme = SeedTenant("Acme");
        db.Tenants.Add(acme);
        await db.SaveChangesAsync();

        interceptor.Armed = true;
        var result = await InfobasesEndpoints.CreateAsync(
            CreateInfobase(acme.Id, "Бухгалтерия"),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(),
            TestHelpers.FixedClock(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.InfobaseAlreadyAssigned);
    }

    [Fact]
    public async Task Create_infobase_tenant_name_race_maps_to_409_NAME_DUPLICATE_IN_TENANT()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            UniqueViolation("IX_Infobases_TenantId_Name", "dbo.Infobases"));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var acme = SeedTenant("Acme");
        db.Tenants.Add(acme);
        await db.SaveChangesAsync();

        interceptor.Armed = true;
        var result = await InfobasesEndpoints.CreateAsync(
            CreateInfobase(acme.Id, "Бухгалтерия"),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(),
            TestHelpers.FixedClock(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.NameDuplicateInTenant);
    }

    [Fact]
    public async Task Update_infobase_tenant_name_race_maps_to_409_NAME_DUPLICATE_IN_TENANT()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            UniqueViolation("IX_Infobases_TenantId_Name", "dbo.Infobases"));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var acme = SeedTenant("Acme");
        db.Tenants.Add(acme);
        var clusterId = Guid.NewGuid();
        var infobase = new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = acme.Id,
            Name = "Бухгалтерия",
            ClusterInfobaseId = clusterId,
            DatabaseName = "ib",
            Status = InfobaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        db.Infobases.Add(infobase);
        db.Publications.Add(new Publication
        {
            Id = Guid.NewGuid(),
            InfobaseId = infobase.Id,
            SiteName = "Default Web Site",
            VirtualPath = "/ib",
            PlatformVersion = "8.3.23.1865",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        interceptor.Armed = true;
        var result = await InfobasesEndpoints.UpdateAsync(
            infobase.Id,
            UpdateInfobase("Зарплата", clusterId),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(),
            TestHelpers.FixedClock(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.NameDuplicateInTenant);
    }

    [Fact]
    public async Task Reassign_infobase_name_race_maps_to_409_INFOBASE_NAME_TAKEN_IN_TARGET()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            UniqueViolation("IX_Infobases_TenantId_Name", "dbo.Infobases"));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var acme = SeedTenant("Acme");
        var beta = SeedTenant("Beta");
        db.Tenants.AddRange(acme, beta);
        var infobase = new Infobase
        {
            Id = Guid.NewGuid(),
            TenantId = acme.Id,
            Name = "Бухгалтерия",
            ClusterInfobaseId = Guid.NewGuid(),
            DatabaseName = "ib",
            Status = InfobaseStatus.Active,
            CreatedAt = DateTime.UtcNow,
        };
        db.Infobases.Add(infobase);
        db.Publications.Add(new Publication
        {
            Id = Guid.NewGuid(),
            InfobaseId = infobase.Id,
            SiteName = "Default Web Site",
            VirtualPath = "/ib",
            PlatformVersion = "8.3.23.1865",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        interceptor.Armed = true;
        var result = await InfobasesEndpoints.ReassignAsync(
            infobase.Id,
            new ReassignInfobaseRequest(beta.Id),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(),
            TestHelpers.FixedClock(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.InfobaseNameTakenInTarget);
    }

    [Fact]
    public async Task Create_tenant_name_race_maps_to_409_NAME_DUPLICATE()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            UniqueViolation("IX_Tenants_Name", "dbo.Tenants"));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);

        interceptor.Armed = true;
        var result = await TenantsEndpoints.CreateAsync(
            new CreateTenantRequest("Acme", 10, true),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(),
            TestHelpers.FixedClock(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.NameDuplicate);
    }

    [Fact]
    public async Task Update_tenant_name_race_maps_to_409_NAME_DUPLICATE()
    {
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            UniqueViolation("IX_Tenants_Name", "dbo.Tenants"));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var acme = SeedTenant("Acme");
        db.Tenants.Add(acme);
        await db.SaveChangesAsync();

        interceptor.Armed = true;
        var result = await TenantsEndpoints.UpdateAsync(
            acme.Id,
            new UpdateTenantRequest("Beta", 10, true),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(),
            TestHelpers.FixedClock(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<Conflict<ProblemDetails>>().Subject;
        conflict.Value!.Extensions["code"].Should().Be(ProblemCodes.NameDuplicate);
    }

    [Fact]
    public async Task Unrecognized_DbUpdateException_is_not_swallowed_propagates_to_global_handler()
    {
        // Не-уникальное нарушение (напр. FK) НЕ должно маппиться в 409 — оно пробрасывается
        // дальше, где глобальный UseExceptionHandler превратит его в 500 ProblemDetails.
        var interceptor = new TestHelpers.ThrowOnSaveInterceptor(
            new DbUpdateException("boom", new InvalidOperationException("The DELETE statement conflicted with the REFERENCE constraint")));
        using var db = TestHelpers.NewInMemoryDb(interceptor: interceptor);
        var acme = SeedTenant("Acme");
        db.Tenants.Add(acme);
        await db.SaveChangesAsync();

        interceptor.Armed = true;
        var act = async () => await InfobasesEndpoints.CreateAsync(
            CreateInfobase(acme.Id, "Бухгалтерия"),
            db,
            new TestHelpers.CapturingAuditLogger(),
            TestHelpers.NewHttpContext(),
            TestHelpers.FixedClock(new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
