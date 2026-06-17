using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Infobases;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Domain.Tenants;
using MitLicenseCenter.Infrastructure.Backups.Testing;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Web.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-186a — серверный агрегат сигналов «Требует внимания» (/dashboard/alerts).
public sealed class DashboardAlertsTests
{
    private static ClaimsPrincipal Principal(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], authenticationType: "Test"));

    private static IActiveSessionSnapshotStore MakeStore(IReadOnlyList<SnapshotSessionEntry> items)
    {
        var store = Substitute.For<IActiveSessionSnapshotStore>();
        store.Current().Returns(new SnapshotPayload(
            items, DateTime.UtcNow, TookMs: 5, Source: "Ras", LicenseFactAvailable: true));
        return store;
    }

    private static SnapshotSessionEntry Session(Guid tenantId)
        => new(
            SessionId: Guid.NewGuid(),
            ClusterInfobaseId: Guid.NewGuid(),
            TenantId: tenantId,
            TenantName: "T",
            InfobaseName: "BP",
            AppId: "1CV8C",
            UserName: "u",
            Host: "h",
            LicenseStatus: LicenseStatus.Consuming,
            StartedAtUtc: DateTime.UtcNow);

    private static IClusterClient MakeCluster(
        bool available, IReadOnlyList<ClusterInfobase>? infobases = null)
    {
        var cluster = Substitute.For<IClusterClient>();
        cluster.ListInfobasesAsync(Arg.Any<CancellationToken>()).Returns(
            new ClusterInfobaseDiscoveryResult(
                infobases ?? [], available, available ? null : "RAS недоступен"));
        return cluster;
    }

    // Настройки без папки/сервера бэкапов — диск не настроен (Configured=false).
    private static ISettingsSnapshot NoBackupSettings() => Substitute.For<ISettingsSnapshot>();

    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    private static async Task<DashboardAlertsResponse> RunAsync(
        AppDbContext db,
        ClaimsPrincipal user,
        IActiveSessionSnapshotStore store,
        IClusterClient? cluster = null,
        ISettingsSnapshot? settings = null,
        ISqlBackupService? backup = null,
        UnassignedInfobasesCache? clusterCache = null,
        IMemoryCache? cache = null)
    {
        var result = await DashboardEndpoints.AlertsAsync(
            user, db, store,
            cluster ?? MakeCluster(available: false),
            clusterCache ?? new UnassignedInfobasesCache(),
            settings ?? NoBackupSettings(),
            backup ?? new FakeSqlBackupService(),
            NullLoggerFactory.Instance,
            TimeProvider.System,
            cache ?? NewCache(),
            CancellationToken.None);
        return ((Ok<DashboardAlertsResponse>)result).Value!;
    }

    private static Tenant Tenant(string name, int limit, bool active = true)
        => new() { Id = Guid.NewGuid(), Name = name, MaxConcurrentLicenses = limit, IsActive = active, CreatedAt = DateTime.UtcNow };

    [Fact]
    public async Task Quota_buckets_count_warning_and_danger_disjointly()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var t74 = Tenant("ok-74", 100);
        var t75 = Tenant("warn-75", 100);
        var t89 = Tenant("warn-89", 100);
        var t90 = Tenant("danger-90", 100);
        var t95 = Tenant("danger-95", 100);
        var tZero = Tenant("zerolimit", 0);             // limit=0 → исключён
        var tInactive = Tenant("inactive", 100, active: false); // неактивен → исключён
        db.Tenants.AddRange(t74, t75, t89, t90, t95, tZero, tInactive);
        await db.SaveChangesAsync();

        var sessions = new List<SnapshotSessionEntry>();
        void Add(Tenant t, int n) => sessions.AddRange(Enumerable.Range(0, n).Select(_ => Session(t.Id)));
        Add(t74, 74);
        Add(t75, 75);
        Add(t89, 89);
        Add(t90, 90);
        Add(t95, 95);
        Add(tZero, 10);     // не должно учитываться
        Add(tInactive, 99); // не должно учитываться

        var body = await RunAsync(db, Principal(Roles.Viewer), MakeStore(sessions));

        body.QuotaWarning.Should().Be(2); // 75, 89
        body.QuotaDanger.Should().Be(2);  // 90, 95
    }

    [Fact]
    public async Task Viewer_does_not_see_cluster_drift_and_does_not_poll_cluster()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var cluster = MakeCluster(available: true, infobases: [new ClusterInfobase(Guid.NewGuid(), "A", null)]);

        var body = await RunAsync(db, Principal(Roles.Viewer), MakeStore([]), cluster);

        body.ClusterDrift.Should().BeNull();
        await cluster.DidNotReceive().ListInfobasesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Admin_cluster_drift_counts_unassigned_excluding_panel_and_hidden()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var idA = Guid.NewGuid();   // в кластере и в панели → не unassigned
        var idB = Guid.NewGuid();   // в кластере, скрыта → не unassigned
        var idC = Guid.NewGuid();   // в кластере, нигде → unassigned
        var idX = Guid.NewGuid();   // в панели, нет в кластере → notInCluster

        var tenant = Tenant("T", 10);
        db.Tenants.Add(tenant);
        db.Infobases.AddRange(
            new Infobase { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "assigned", ClusterInfobaseId = idA, DatabaseName = "n", Status = InfobaseStatus.Active, CreatedAt = DateTime.UtcNow },
            new Infobase { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "ghost", ClusterInfobaseId = idX, DatabaseName = "n", Status = InfobaseStatus.Active, CreatedAt = DateTime.UtcNow });
        db.HiddenClusterInfobases.Add(new HiddenClusterInfobase { ClusterInfobaseId = idB, Name = "hidden", HiddenAtUtc = DateTime.UtcNow, HiddenBy = "admin" });
        await db.SaveChangesAsync();

        var cluster = MakeCluster(available: true, infobases:
        [
            new ClusterInfobase(idA, "A", null),
            new ClusterInfobase(idB, "B", null),
            new ClusterInfobase(idC, "C", null),
        ]);

        var body = await RunAsync(db, Principal(Roles.Admin), MakeStore([]), cluster);

        body.ClusterDrift.Should().NotBeNull();
        body.ClusterDrift!.Available.Should().BeTrue();
        body.ClusterDrift.UnassignedBases.Should().Be(1);   // idC
        body.ClusterDrift.BasesNotInCluster.Should().Be(1); // idX
    }

    [Fact]
    public async Task Admin_cluster_drift_unavailable_yields_null_counts()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var cluster = MakeCluster(available: false);

        var body = await RunAsync(db, Principal(Roles.Admin), MakeStore([]), cluster);

        body.ClusterDrift.Should().NotBeNull();
        body.ClusterDrift!.Available.Should().BeFalse();
        body.ClusterDrift.UnassignedBases.Should().BeNull();
        body.ClusterDrift.BasesNotInCluster.Should().BeNull();
    }

    [Fact]
    public async Task Backup_disk_low_when_free_below_margin()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.BackupFolderPath).Returns(@"D:\Backups");
        settings.GetString(SettingKey.SqlServer).Returns("SQL01");
        settings.GetInt(SettingKey.BackupDiskSafetyMarginMb).Returns(2048); // 2 ГиБ
        var backup = new FakeSqlBackupService { NextBackupDiskFreeBytes = 1L * 1024 * 1024 * 1024 }; // 1 ГиБ < 2 ГиБ

        var body = await RunAsync(db, Principal(Roles.Viewer), MakeStore([]), settings: settings, backup: backup);

        body.BackupDisk.Configured.Should().BeTrue();
        body.BackupDisk.FreeBytes.Should().Be(1L * 1024 * 1024 * 1024);
        body.BackupDisk.SafetyMarginBytes.Should().Be(2L * 1024 * 1024 * 1024);
        body.BackupDisk.Low.Should().BeTrue();
    }

    [Fact]
    public async Task Backup_disk_not_low_when_free_above_margin()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.BackupFolderPath).Returns(@"D:\Backups");
        settings.GetString(SettingKey.SqlServer).Returns("SQL01");
        settings.GetInt(SettingKey.BackupDiskSafetyMarginMb).Returns(2048);
        var backup = new FakeSqlBackupService { NextBackupDiskFreeBytes = 50L * 1024 * 1024 * 1024 };

        var body = await RunAsync(db, Principal(Roles.Viewer), MakeStore([]), settings: settings, backup: backup);

        body.BackupDisk.Low.Should().BeFalse();
    }

    [Fact]
    public async Task Backup_disk_not_configured_when_settings_blank()
    {
        using var db = TestHelpers.NewInMemoryDb();

        var body = await RunAsync(db, Principal(Roles.Viewer), MakeStore([]));

        body.BackupDisk.Configured.Should().BeFalse();
        body.BackupDisk.FreeBytes.Should().BeNull();
        body.BackupDisk.Low.Should().BeFalse();
    }

    [Fact]
    public async Task Backup_disk_unknown_free_is_not_low()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetString(SettingKey.BackupFolderPath).Returns(@"D:\Backups");
        settings.GetString(SettingKey.SqlServer).Returns("SQL01");
        // FakeSqlBackupService по умолчанию NextBackupDiskFreeBytes=null → «не знаем».
        var body = await RunAsync(db, Principal(Roles.Viewer), MakeStore([]), settings: settings);

        body.BackupDisk.Configured.Should().BeTrue();
        body.BackupDisk.FreeBytes.Should().BeNull();
        body.BackupDisk.Low.Should().BeFalse();
    }

    [Fact]
    public async Task Cache_short_circuits_cluster_poll_within_ttl_per_role()
    {
        using var db = TestHelpers.NewInMemoryDb();
        var cluster = MakeCluster(available: true, infobases: [new ClusterInfobase(Guid.NewGuid(), "A", null)]);
        var clusterCache = new UnassignedInfobasesCache();
        var cache = NewCache();
        var store = MakeStore([]);

        await RunAsync(db, Principal(Roles.Admin), store, cluster, clusterCache: clusterCache, cache: cache);
        await RunAsync(db, Principal(Roles.Admin), store, cluster, clusterCache: clusterCache, cache: cache);

        // Второй вызов в пределах 30-секундного TTL берёт ответ из кэша алертов — кластер
        // опрашивается ровно один раз.
        await cluster.Received(1).ListInfobasesAsync(Arg.Any<CancellationToken>());
    }
}
