using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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
        });

        builder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Timestamp).IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.ActionType).IsRequired().HasMaxLength(100);
            e.Property(x => x.Initiator).IsRequired().HasMaxLength(256);
            e.Property(x => x.Description).IsRequired();
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.ActionType);
        });
    }
}
