using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-185c (зеркало LicenseUsageRetentionJobTests): ретеншен телеметрии размера баз —
// батчевое удаление DatabaseSizeSnapshots старше cutoff (now - Settings.DatabaseSize.
// RetentionDays). Реальный реляционный провайдер (SQLite) — provider-portable ExecuteDelete
// транслируется и сюда, и на прод-MSSQL.
public sealed class DatabaseSizeRetentionJobTests
{
    private static readonly DateTime Now = new(2026, 6, 17, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Deletes_only_snapshots_older_than_cutoff()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var settings = SettingsWithRetention(30); // cutoff = Now - 30д = 2026-05-18 04:00

        var old1 = Snap(Now.AddDays(-40));
        var old2 = Snap(Now.AddDays(-31));
        var keep1 = Snap(Now.AddDays(-29));
        var keep2 = Snap(Now.AddDays(-1));
        using (var seed = sqlite.NewContext())
        {
            seed.DatabaseSizeSnapshots.AddRange(old1, old2, keep1, keep2);
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, settings).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.DatabaseSizeSnapshots.Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([keep1.Id, keep2.Id],
                "удаляются только снимки старше cutoff");
    }

    [Fact]
    public async Task Deletes_all_old_rows_across_multiple_batches()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var settings = SettingsWithRetention(30);

        // 5001 > BatchSize(5000) → do-while делает минимум два прохода.
        const int count = 5001;
        var oldTime = Now.AddDays(-60);
        using (var seed = sqlite.NewContext())
        {
            for (var i = 0; i < count; i++)
                seed.DatabaseSizeSnapshots.Add(Snap(oldTime));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, settings).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.DatabaseSizeSnapshots.Count().Should().Be(0,
            "все старые строки удалены за несколько батчей");
    }

    [Fact]
    public async Task Does_nothing_when_no_rows_are_old_enough()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var settings = SettingsWithRetention(365); // дефолт

        using (var seed = sqlite.NewContext())
        {
            seed.DatabaseSizeSnapshots.Add(Snap(Now.AddDays(-10)));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, settings).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.DatabaseSizeSnapshots.Count().Should().Be(1);
    }

    // MLC-074 (регресс): на проде включён EnableRetryOnFailure
    // (DependencyInjection.cs → SqlServerRetryingExecutionStrategy). Джоба открывает
    // транзакцию внутри CreateExecutionStrategy().ExecuteAsync — тут навешиваем стратегию с
    // RetriesOnFailure=true, воспроизводя прод-гард: без обёртки RunAsync падал бы
    // InvalidOperationException ("...does not support user-initiated transactions"). Дефолтный
    // SQLite (NonRetryingExecutionStrategy) этот путь не ловит.
    [Fact]
    public async Task Deletes_old_rows_under_a_retrying_execution_strategy()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create(
            o => o.ExecutionStrategy(d => new TestHelpers.RetriesOnFailureExecutionStrategy(d)));
        var settings = SettingsWithRetention(30);

        var old1 = Snap(Now.AddDays(-40));
        var keep1 = Snap(Now.AddDays(-1));
        using (var seed = sqlite.NewContext())
        {
            seed.DatabaseSizeSnapshots.AddRange(old1, keep1);
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, settings).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.DatabaseSizeSnapshots.Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([keep1.Id],
                "ретеншен отрабатывает и при ретраящей стратегии (MLC-074)");
    }

    private static DatabaseSizeRetentionJob NewJob(
        Infrastructure.Persistence.AppDbContext db, ISettingsSnapshot settings) =>
        new(db, settings, TestHelpers.FixedClock(Now), NullLogger<DatabaseSizeRetentionJob>.Instance);

    private static ISettingsSnapshot SettingsWithRetention(int days)
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(SettingKey.DatabaseSizeRetentionDays).Returns(days);
        return settings;
    }

    // TenantId=null → не нужна строка Tenant (FK SetNull допускает null).
    private static DatabaseSizeSnapshot Snap(DateTime snapshotAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = null,
        DatabaseName = "db",
        SnapshotAtUtc = snapshotAtUtc,
        DataBytes = 1000,
        LogBytes = 100,
    };
}
