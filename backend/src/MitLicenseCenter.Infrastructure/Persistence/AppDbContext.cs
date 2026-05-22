using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Infobase> Infobases => Set<Infobase>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SettingEntry> Settings => Set<SettingEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ASP.NET Core Identity tables → schema "auth" (docs/03 §6).
        builder.Entity<AppUser>().ToTable("Users", "auth");
        builder.Entity<AppRole>().ToTable("Roles", "auth");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("UserRoles", "auth");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("UserClaims", "auth");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("UserLogins", "auth");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("UserTokens", "auth");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("RoleClaims", "auth");

        builder.Entity<Tenant>(e =>
        {
            e.ToTable("Tenants", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.MaxConcurrentLicenses).IsRequired();
            e.Property(x => x.IsActive).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt);
            e.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<Infobase>(e =>
        {
            e.ToTable("Infobases", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.TenantId).IsRequired();
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.ClusterInfobaseId).IsRequired();
            e.Property(x => x.DatabaseServer).IsRequired().HasMaxLength(200);
            e.Property(x => x.DatabaseName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<int>().IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt);
            // Имя инфобазы уникально в пределах клиента — два разных клиента могут иметь
            // одноимённые базы (например, «Бухгалтерия»), но один клиент — нет.
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            // Restrict: Infobase — часть aggregate Tenant'а, удаление tenant'а
            // с непустым набором инфобаз блокируется guard'ом в endpoint'е (409),
            // SQL Server поднимет FK violation как fallback.
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Publication>(e =>
        {
            e.ToTable("Publications", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.InfobaseId).IsRequired();
            e.Property(x => x.SiteName).IsRequired().HasMaxLength(200);
            e.Property(x => x.VirtualPath).IsRequired().HasMaxLength(200);
            e.Property(x => x.PlatformVersion).IsRequired().HasMaxLength(50);
            e.Property(x => x.EnableOData).IsRequired();
            e.Property(x => x.EnableHttpServices).IsRequired();
            e.Property(x => x.VrdCustomXml);
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt);
            // Drift-поля (PR 3.5). LastDriftStatus = int с дефолтом InSync=0
            // (миграция Stage3PublicationDrift ставит DEFAULT 0 на уровне БД).
            e.Property(x => x.LastDriftStatus).HasConversion<int>().IsRequired();
            e.Property(x => x.LastDriftCheckAt);
            e.Property(x => x.LastDriftDetails);
            // Physical-path override (PR 4.1): nullable, max 260 (MAX_PATH).
            e.Property(x => x.PhysicalPathOverride).HasMaxLength(260);
            // 1-to-1 required: Publication — часть aggregate Infobase'а; удаление
            // инфобазы каскадом сносит публикацию в БД (IIS-unpublish — Stage 3).
            e.HasOne<Infobase>()
                .WithOne()
                .HasForeignKey<Publication>(x => x.InfobaseId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Timestamp).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.ActionType).HasConversion<int>().IsRequired();
            e.Property(x => x.Reason).HasConversion<int?>();
            e.Property(x => x.Initiator).IsRequired().HasMaxLength(256);
            e.Property(x => x.Description).IsRequired();
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.ActionType);
            // SetNull: tenant deletion обнуляет ссылку, но запись аудита остаётся —
            // история всегда сохраняется.
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<SettingEntry>(e =>
        {
            e.ToTable("Settings", "dbo");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).IsRequired().HasMaxLength(200);
            // ValueText: plain payload; Value: DPAPI-зашифрованные UTF-8 байты.
            // Ровно один из двух не-null в любой момент времени.
            e.Property(x => x.ValueText);
            e.Property(x => x.Value).HasColumnType("varbinary(max)");
            e.Property(x => x.IsSecret).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.UpdatedAt).IsRequired();
            e.Property(x => x.UpdatedBy).IsRequired().HasMaxLength(256);
        });
    }
}
