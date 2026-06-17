using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Audit;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Reporting;

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
    public DbSet<LicenseUsageSnapshot> LicenseUsageSnapshots => Set<LicenseUsageSnapshot>();
    public DbSet<DatabaseSizeSnapshot> DatabaseSizeSnapshots => Set<DatabaseSizeSnapshot>();
    public DbSet<PerfRecording> PerfRecordings => Set<PerfRecording>();
    public DbSet<PerfRecordingSample> PerfRecordingSamples => Set<PerfRecordingSample>();
    public DbSet<DatabaseBackup> DatabaseBackups => Set<DatabaseBackup>();
    public DbSet<HiddenClusterInfobase> HiddenClusterInfobases => Set<HiddenClusterInfobase>();

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
            // MLC-136 (R12c) — оптимистическая блокировка. IsRowVersion() маппит на SQL
            // Server тип `rowversion`, помечает свойство IsConcurrencyToken и
            // ValueGeneratedOnAddOrUpdate (БД генерирует значение при INSERT/UPDATE).
            // На конкурентном UPDATE с устаревшим OriginalValue EF бросает
            // DbUpdateConcurrencyException → endpoint мапит в 409.
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<Infobase>(e =>
        {
            e.ToTable("Infobases", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.TenantId).IsRequired();
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.ClusterInfobaseId).IsRequired();
            e.Property(x => x.DatabaseName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<int>().IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt);
            // MLC-151 — оптимистическая блокировка (зеркаль Tenant/MLC-136). IsRowVersion()
            // маппит на SQL Server тип `rowversion`, помечает свойство IsConcurrencyToken и
            // ValueGeneratedOnAddOrUpdate. На конкурентном UPDATE с устаревшим OriginalValue
            // EF бросает DbUpdateConcurrencyException → endpoint мапит в 409.
            e.Property(x => x.RowVersion).IsRowVersion();
            // Имя инфобазы уникально в пределах клиента — два разных клиента могут иметь
            // одноимённые базы (например, «Бухгалтерия»), но один клиент — нет.
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            // Одна база кластера может принадлежать только одному клиенту (глобальная уникальность).
            e.HasIndex(x => x.ClusterInfobaseId).IsUnique();
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
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt);
            // Происхождение (MLC-045): int с дефолтом Unknown=0.
            e.Property(x => x.Source).HasConversion<int>().IsRequired();
            // Read-only статус (MLC-045): int с дефолтом Unknown=0 (миграция ставит
            // DEFAULT 0 на уровне БД). Заполняется проверкой/refresh-job'ом.
            e.Property(x => x.LastCheckStatus).HasConversion<int>().IsRequired();
            e.Property(x => x.LastCheckAt);
            e.Property(x => x.LastCheckDetails);
            // Physical-path override (PR 4.1): nullable, max 260 (MAX_PATH).
            e.Property(x => x.PhysicalPathOverride).HasMaxLength(260);
            // MLC-151 — оптимистическая блокировка (зеркаль Tenant/MLC-136). Собственный
            // токен, т.к. у публикации есть самостоятельный PUT /publications/{id} помимо
            // вложенного апдейта через aggregate инфобазы.
            e.Property(x => x.RowVersion).IsRowVersion();
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
            // Составной под дорогой растущий запрос /audit: фильтр по TenantId +
            // ORDER BY Timestamp DESC, Id DESC (PERF-06/MLC-042). Ключ (TenantId ASC,
            // Timestamp DESC, Id DESC) убирает Sort и key lookup; лидирующий TenantId
            // покрывает FK-seek, поэтому конвенция FK не создаёт одноколоночный
            // IX_AuditLogs_TenantId. INCLUDE не нужен: остаточный lookup ограничен
            // размером страницы (Top N), а Description (nvarchar(max)) раздул бы индекс.
            e.HasIndex(x => new { x.TenantId, x.Timestamp, x.Id })
                .IsDescending(false, true, true)
                .HasDatabaseName("IX_AuditLogs_TenantId_Timestamp_Id");
            // SetNull: tenant deletion обнуляет ссылку, но запись аудита остаётся —
            // история всегда сохраняется.
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<LicenseUsageSnapshot>(e =>
        {
            // MLC-048 (ADR-25): телеметрия использования лицензий — агрегат на тенанта за
            // 15-мин бакет. Конфиг inline по паттерну AuditLog (сущность-телеметрия).
            e.ToTable("LicenseUsageSnapshots", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.BucketStartUtc).IsRequired();
            e.Property(x => x.ConsumedMin).IsRequired();
            e.Property(x => x.ConsumedMax).IsRequired();
            e.Property(x => x.ConsumedAvg).IsRequired();
            e.Property(x => x.Limit).IsRequired();
            // Дорога чтения отчётов (MLC-049): фильтр по TenantId + диапазон BucketStartUtc.
            e.HasIndex(x => new { x.TenantId, x.BucketStartUtc });
            // SetNull (как AuditLog): удаление тенанта обнуляет ссылку, но замеры остаются —
            // история использования переживает удаление клиента.
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DatabaseSizeSnapshot>(e =>
        {
            // MLC-185: снимок размера базы данных. Конфиг inline по паттерну
            // LicenseUsageSnapshot (сущность-телеметрия). DatabaseName — ключ
            // сопоставления (как у DatabaseBackup), max 200 — как Infobase.DatabaseName.
            e.ToTable("DatabaseSizeSnapshots", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.DatabaseName).IsRequired().HasMaxLength(200);
            e.Property(x => x.SnapshotAtUtc).IsRequired();
            e.Property(x => x.DataBytes).IsRequired();
            e.Property(x => x.LogBytes).IsRequired();
            // Дорога чтения отчётов: ряд по базе во времени + срез по клиенту во времени.
            e.HasIndex(x => new { x.DatabaseName, x.SnapshotAtUtc });
            e.HasIndex(x => new { x.TenantId, x.SnapshotAtUtc });
            // SetNull (как LicenseUsageSnapshot): удаление тенанта обнуляет ссылку,
            // но замеры остаются — история размера переживает удаление клиента.
            e.HasOne<Tenant>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PerfRecording>(e =>
        {
            // MLC-070 (ADR-26, Фаза 4): запись быстродействия по требованию. Сущность-телеметрия,
            // конфиг inline по паттерну LicenseUsageSnapshot. Без FK на Tenant — запись охватывает
            // весь хост, а не клиента.
            e.ToTable("PerfRecordings", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.StartedAtUtc).IsRequired();
            e.Property(x => x.StoppedAtUtc);
            // Enum'ы как int (HasConversion) по конвенции проекта (InfobaseStatus/Publication.*).
            e.Property(x => x.Status).HasConversion<int>().IsRequired();
            e.Property(x => x.StopReason).HasConversion<int?>();
            e.Property(x => x.StartedBy).IsRequired().HasMaxLength(256);
            // Список расследований сортируется по началу (свежие сверху).
            e.HasIndex(x => x.StartedAtUtc);
        });

        builder.Entity<PerfRecordingSample>(e =>
        {
            // MLC-070: один сэмпл записи. Host-метрики уровня 1 — плоскими колонками; атрибуция по
            // семьям и точечные топ-виновники 1С/SQL — в JSON-колонках (nvarchar(max)).
            e.ToTable("PerfRecordingSamples", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.RecordingId).IsRequired();
            e.Property(x => x.SampleUtc).IsRequired();
            e.Property(x => x.Measuring).IsRequired();
            e.Property(x => x.CpuPercent).IsRequired();
            e.Property(x => x.CpuQueueLength).IsRequired();
            e.Property(x => x.MemoryAvailableMBytes).IsRequired();
            e.Property(x => x.MemoryTotalMBytes).IsRequired();
            e.Property(x => x.MemoryPagesPerSec).IsRequired();
            e.Property(x => x.DiskAvgReadSecPerOp).IsRequired();
            e.Property(x => x.DiskAvgWriteSecPerOp).IsRequired();
            e.Property(x => x.DiskQueueLength).IsRequired();
            e.Property(x => x.ProcessesInaccessible).IsRequired();
            e.Property(x => x.ProcessGroupsJson).IsRequired();
            e.Property(x => x.OneCLoadJson);
            e.Property(x => x.SqlLoadJson);
            // Ряд сэмплов записи читается отсортированным по времени.
            e.HasIndex(x => new { x.RecordingId, x.SampleUtc });
            // Cascade: удаление записи (DELETE /recordings/{id}) сносит её сэмплы.
            e.HasOne<PerfRecording>()
                .WithMany(r => r.Samples)
                .HasForeignKey(x => x.RecordingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DatabaseBackup>(e =>
        {
            // MLC-076 (ADR-27): учёт/очередь бэкапов баз SQL. Сущность-телеметрия, конфиг inline
            // по паттерну PerfRecording. Без FK на Infobase — запись переживает удаление инфобазы
            // (InfobaseId простым Guid, как у LicenseUsageSnapshot).
            e.ToTable("DatabaseBackups", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.InfobaseId).IsRequired();
            e.Property(x => x.DatabaseServer).IsRequired().HasMaxLength(200);
            e.Property(x => x.DatabaseName).IsRequired().HasMaxLength(200);
            // Enum'ы как int (HasConversion) по конвенции проекта (PerfRecording/InfobaseStatus).
            e.Property(x => x.Status).HasConversion<int>().IsRequired();
            e.Property(x => x.RequestedBy).IsRequired().HasMaxLength(256);
            e.Property(x => x.RequestedAtUtc).IsRequired();
            e.Property(x => x.StartedAtUtc);
            e.Property(x => x.CompletedAtUtc);
            e.Property(x => x.FilePath).HasMaxLength(512);
            e.Property(x => x.FileSizeBytes);
            e.Property(x => x.FailureReason).HasConversion<int>().IsRequired();
            e.Property(x => x.ErrorMessage);
            // Список бэкапов сортируется по времени запроса (свежие сверху).
            e.HasIndex(x => x.RequestedAtUtc);
            // Дорога насоса (MLC-077): «есть ли Queued/Running для этой пары server+db» +
            // выборка самой старой Queued.
            e.HasIndex(x => new { x.DatabaseServer, x.DatabaseName, x.Status });
        });

        builder.Entity<HiddenClusterInfobase>(e =>
        {
            // MLC-092: игнор-лист «нераспределённых» баз кластера. PK — ClusterInfobaseId
            // (одна запись на базу, без суррогатного Id). Без FK: база кластера панели
            // не принадлежит — это снапшот решения оператора «служебная, не показывать».
            // Name — снапшот для рендера блока «Скрытые» без обращения к RAS; длина 200 —
            // как Infobase.Name. HiddenBy — как Initiator аудита (256).
            e.ToTable("HiddenClusterInfobases", "dbo");
            e.HasKey(x => x.ClusterInfobaseId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.HiddenAtUtc).IsRequired();
            e.Property(x => x.HiddenBy).IsRequired().HasMaxLength(256);
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

    // UTC на проводе: колонки datetime2 не несут зоны, поэтому при чтении EF отдаёт
    // DateTime с Kind=Unspecified, а System.Text.Json сериализует такое значение БЕЗ
    // суффикса `Z`. Браузерный new Date() трактует строку без `Z` как локальное время
    // → метка уезжает на величину часового пояса. В БД всё пишется в UTC
    // (GetUtcNow().UtcDateTime), поэтому помечаем все DateTime/DateTime? как Utc при
    // материализации — JSON получает `Z`, фронт парсит корректно. Конвертер read-only
    // (запись identity), тип колонки не меняется → миграция не нужна.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter()
            : base(
                v => v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }
}
