using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Infrastructure.Reporting;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-048 (ADR-25): ретеншен телеметрии — батчевое удаление LicenseUsageSnapshots старше
// cutoff (now - Settings.LicenseUsage.RetentionDays). Реальный реляционный провайдер
// (SQLite) — provider-portable ExecuteDelete транслируется и сюда, и на прод-MSSQL.
public sealed class LicenseUsageRetentionJobTests
{
    private static readonly DateTime Now = new(2026, 6, 6, 3, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Deletes_only_snapshots_older_than_cutoff()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var settings = SettingsWithRetention(30); // cutoff = Now - 30д = 2026-05-07 03:30

        var old1 = Snap(Now.AddDays(-40));
        var old2 = Snap(Now.AddDays(-31));
        var keep1 = Snap(Now.AddDays(-29));
        var keep2 = Snap(Now.AddDays(-1));
        using (var seed = sqlite.NewContext())
        {
            seed.LicenseUsageSnapshots.AddRange(old1, old2, keep1, keep2);
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, settings).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.LicenseUsageSnapshots.Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([keep1.Id, keep2.Id],
                "удаляются только замеры старше cutoff");
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
                seed.LicenseUsageSnapshots.Add(Snap(oldTime));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, settings).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.LicenseUsageSnapshots.Count().Should().Be(0,
            "все старые строки удалены за несколько батчей");
    }

    [Fact]
    public async Task Does_nothing_when_no_rows_are_old_enough()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var settings = SettingsWithRetention(365); // дефолт

        using (var seed = sqlite.NewContext())
        {
            seed.LicenseUsageSnapshots.Add(Snap(Now.AddDays(-10)));
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, settings).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.LicenseUsageSnapshots.Count().Should().Be(1);
    }

    // MLC-074 (регресс): на проде включён EnableRetryOnFailure
    // (DependencyInjection.cs → SqlServerRetryingExecutionStrategy). Джоба открывала
    // транзакцию вручную (BeginTransactionAsync) вне CreateExecutionStrategy().ExecuteAsync,
    // что на ретраящей стратегии бросает "...does not support user-initiated transactions"
    // → телеметрия не чистилась. Тут навешиваем стратегию с RetriesOnFailure=true,
    // воспроизводя прод-гард: до фикса RunAsync падал InvalidOperationException, после —
    // удаляет штатно. Дефолтный SQLite (NonRetryingExecutionStrategy) этот путь не ловит.
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
            seed.LicenseUsageSnapshots.AddRange(old1, keep1);
            await seed.SaveChangesAsync();
        }

        using (var ctx = sqlite.NewContext())
        {
            await NewJob(ctx, settings).RunAsync(CancellationToken.None);
        }

        using var verify = sqlite.NewContext();
        verify.LicenseUsageSnapshots.Select(x => x.Id).ToList()
            .Should().BeEquivalentTo([keep1.Id],
                "ретеншен отрабатывает и при ретраящей стратегии (MLC-074)");
    }

    private static LicenseUsageRetentionJob NewJob(
        Infrastructure.Persistence.AppDbContext db, ISettingsSnapshot settings) =>
        new(db, settings, TestHelpers.FixedClock(Now), NullLogger<LicenseUsageRetentionJob>.Instance);

    private static ISettingsSnapshot SettingsWithRetention(int days)
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(SettingKey.LicenseUsageRetentionDays).Returns(days);
        return settings;
    }

    // TenantId=null → не нужна строка Tenant (FK SetNull допускает null).
    private static LicenseUsageSnapshot Snap(DateTime bucketStartUtc) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = null,
        BucketStartUtc = bucketStartUtc,
        ConsumedMin = 0,
        ConsumedMax = 1,
        ConsumedAvg = 0.5,
        Limit = 10,
    };
}
