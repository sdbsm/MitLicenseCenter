using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Audit;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-008 — Контрактные тесты persistence-инвариантов на РЕАЛЬНОМ провайдере БД
// (SQLite-in-memory, схема из той же модели через EnsureCreated). В отличие от EF
// InMemory (NewInMemoryDb), который игнорирует unique-индексы, FK-поведение и
// конкурентность, здесь инварианты валидируются именно на стороне СУБД.
//
// Канон: docs/03_DOMAIN_MODEL.md «Persistence & API Contracts (binding)» → раздел
// «Foreign keys»; AppDbContext.OnModelCreating.
public sealed class PersistenceContractTests
{
    // ── Уникальность имени инфобазы в пределах клиента (IX_Infobases_TenantId_Name) ──

    [Fact]
    public void Infobase_name_must_be_unique_within_a_tenant()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var tenant = NewTenant("Acme");
        db.Tenants.Add(tenant);
        db.Infobases.Add(NewInfobase(tenant.Id, "Бухгалтерия"));
        db.SaveChanges();

        db.Infobases.Add(NewInfobase(tenant.Id, "Бухгалтерия"));

        var act = () => db.SaveChanges();

        act.Should().Throw<DbUpdateException>(
            "IX_Infobases_TenantId_Name запрещает две одноимённые базы у одного клиента");
    }

    [Fact]
    public void Same_infobase_name_is_allowed_for_different_tenants()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var acme = NewTenant("Acme");
        var globex = NewTenant("Globex");
        db.Tenants.AddRange(acme, globex);
        db.Infobases.Add(NewInfobase(acme.Id, "Бухгалтерия"));
        db.SaveChanges();

        db.Infobases.Add(NewInfobase(globex.Id, "Бухгалтерия"));

        var act = () => db.SaveChanges();

        act.Should().NotThrow("уникальность имени — в пределах клиента, не глобальная");
    }

    // ── Глобальная уникальность кластер-базы (IX_Infobases_ClusterInfobaseId) ──

    [Fact]
    public void ClusterInfobaseId_must_be_globally_unique_across_tenants()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        using var db = sqlite.NewContext();

        var acme = NewTenant("Acme");
        var globex = NewTenant("Globex");
        db.Tenants.AddRange(acme, globex);

        var clusterId = Guid.NewGuid();
        var first = NewInfobase(acme.Id, "Acme BP");
        first.ClusterInfobaseId = clusterId;
        db.Infobases.Add(first);
        db.SaveChanges();

        var second = NewInfobase(globex.Id, "Globex BP");
        second.ClusterInfobaseId = clusterId;
        db.Infobases.Add(second);

        var act = () => db.SaveChanges();

        act.Should().Throw<DbUpdateException>(
            "IX_Infobases_ClusterInfobaseId: одна база кластера принадлежит ровно одному клиенту");
    }

    // ── FK Publication → Infobase = Cascade ──

    [Fact]
    public void Deleting_an_infobase_cascades_the_publication_at_the_database_level()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var tenant = NewTenant("Acme");
        var infobase = NewInfobase(tenant.Id, "Acme BP");
        var publication = NewPublication(infobase.Id);
        using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(tenant);
            seed.Infobases.Add(infobase);
            seed.Publications.Add(publication);
            seed.SaveChanges();
        }

        // Чистый контекст: НЕ отслеживает публикацию, поэтому каскад выполняет СУБД,
        // а не change-tracker EF.
        using (var del = sqlite.NewContext())
        {
            var tracked = del.Infobases.Single(x => x.Id == infobase.Id);
            del.Infobases.Remove(tracked);
            del.SaveChanges();
        }

        using var verify = sqlite.NewContext();
        verify.Infobases.Count().Should().Be(0);
        verify.Publications.Count().Should().Be(0,
            "FK Publication→Infobase = Cascade: удаление базы сносит публикацию на стороне БД");
    }

    // ── FK Infobase → Tenant = Restrict ──

    [Fact]
    public void Deleting_a_tenant_that_still_has_infobases_is_blocked_by_the_database()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var tenant = NewTenant("Acme");
        var infobase = NewInfobase(tenant.Id, "Acme BP");
        using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(tenant);
            seed.Infobases.Add(infobase);
            seed.SaveChanges();
        }

        using (var del = sqlite.NewContext())
        {
            var tracked = del.Tenants.Single(x => x.Id == tenant.Id);
            del.Tenants.Remove(tracked);

            var act = () => del.SaveChanges();

            act.Should().Throw<DbUpdateException>(
                "FK Infobase→Tenant = Restrict: БД блокирует удаление клиента с базами");
        }

        using var verify = sqlite.NewContext();
        verify.Infobases.Count().Should().Be(1, "база осталась — удаление клиента отклонено");
        verify.Tenants.Count().Should().Be(1);
    }

    // ── FK AuditLogs.TenantId = SetNull ──

    [Fact]
    public void Deleting_a_tenant_nulls_the_audit_log_reference_but_keeps_the_row()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();

        var tenant = NewTenant("Acme");
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            ActionType = AuditActionType.TenantCreated,
            Initiator = "admin",
            Description = "Создан клиент «Acme»",
            TenantId = tenant.Id,
        };
        using (var seed = sqlite.NewContext())
        {
            seed.Tenants.Add(tenant);
            seed.AuditLogs.Add(audit);
            seed.SaveChanges();
        }

        // Чистый контекст: SetNull выполняет СУБД (запись аудита не отслеживается).
        using (var del = sqlite.NewContext())
        {
            var tracked = del.Tenants.Single(x => x.Id == tenant.Id);
            del.Tenants.Remove(tracked);
            del.SaveChanges();
        }

        using var verify = sqlite.NewContext();
        verify.Tenants.Count().Should().Be(0);
        var row = verify.AuditLogs.Single(x => x.Id == audit.Id);
        row.TenantId.Should().BeNull(
            "AuditLogs.TenantId = SetNull: история аудита переживает удаление клиента");
    }

    // ── builders ──

    private static Tenant NewTenant(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        MaxConcurrentLicenses = 10,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private static Infobase NewInfobase(Guid tenantId, string name) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = name,
        ClusterInfobaseId = Guid.NewGuid(),
        DatabaseServer = "sql.local",
        DatabaseName = "db",
        Status = InfobaseStatus.Active,
        CreatedAt = DateTime.UtcNow,
    };

    private static Publication NewPublication(Guid infobaseId) => new()
    {
        Id = Guid.NewGuid(),
        InfobaseId = infobaseId,
        SiteName = "Default Web Site",
        VirtualPath = "/acme-bp",
        PlatformVersion = "8.3.23.1865",
        EnableOData = false,
        EnableHttpServices = false,
        CreatedAt = DateTime.UtcNow,
    };
}
