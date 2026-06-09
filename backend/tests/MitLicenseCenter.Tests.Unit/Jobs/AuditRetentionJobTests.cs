using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Jobs;
using MitLicenseCenter.Tests.Unit.Endpoints;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-074 (регресс): ретеншен аудита. Удаление сделано raw
// `DELETE TOP (5000) FROM dbo.AuditLogs` (MSSQL-only синтаксис), и в EF Core 10 raw
// `ExecuteSql` ОБХОДИТ execution strategy (идёт прямо в RelationalCommand). Поэтому до
// фикса конфигурированная стратегия для этой джобы не вызывается ни разу (manual tx +
// raw SQL её минуют), а батч не получает retry-защиты и не обёрнут как retriable-юнит.
// После фикса батч идёт через CreateExecutionStrategy().ExecuteAsync → стратегия
// вызывается. Дискриминатор бага — счётчик запусков стратегии (>0 только после фикса).
// Сам `DELETE TOP` на SQLite не парсится → SqliteException (ожидаемо; happy-path
// раздела покрыт на живом SQL-стенде, см. отчёт MLC-074).
public sealed class AuditRetentionJobTests
{
    private static readonly DateTime Now = new(2026, 6, 9, 3, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Runs_the_batch_through_the_execution_strategy()
    {
        var strategyRuns = 0;
        using var sqlite = TestHelpers.SqliteTestDb.Create(
            o => o.ExecutionStrategy(d => new TestHelpers.RetriesOnFailureExecutionStrategy(
                d, onFirstExecution: () => strategyRuns++)));
        var settings = SettingsWithRetention(30);
        var audit = new TestHelpers.CapturingAuditLogger();

        using var ctx = sqlite.NewContext();
        var job = new AuditRetentionJob(
            ctx, audit, settings, TestHelpers.FixedClock(Now),
            NullLogger<AuditRetentionJob>.Instance);

        // Сбрасываем счётчик: EnsureCreated при создании SQLite-БД тоже гоняется через
        // стратегию. Считаем только запуски, инициированные самой джобой.
        strategyRuns = 0;

        // `DELETE TOP … dbo.AuditLogs` на SQLite не парсится → SqliteException; нам важен
        // факт, что выполнение шло ЧЕРЕЗ стратегию (а не в обход, как до фикса).
        var act = async () => await job.RunAsync(CancellationToken.None);
        await act.Should().ThrowAsync<SqliteException>();

        strategyRuns.Should().BeGreaterThan(0,
            "батч обёрнут в CreateExecutionStrategy().ExecuteAsync — иначе на проде raw-SQL " +
            "идёт мимо ретраящей стратегии и без retry-защиты (MLC-074)");
        audit.Entries.Should().BeEmpty("аудит пишется только при успешном удалении");
    }

    private static ISettingsSnapshot SettingsWithRetention(int days)
    {
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(SettingKey.AuditRetentionDays).Returns(days);
        return settings;
    }
}
