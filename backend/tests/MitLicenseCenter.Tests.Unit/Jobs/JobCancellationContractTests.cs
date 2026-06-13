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

// MLC-123 (BE-20): характеризующий тест graceful-shutdown контракта. Hangfire 1.8 при
// выполнении job'ы ПОДМЕНЯЕТ плейсхолдер CancellationToken.None (из выражения
// RecurringJob.AddOrUpdate<T>(j => j.RunAsync(CancellationToken.None))) реальным токеном,
// сигналящимся при остановке сервера / abort'е. Контракт держится ТОЛЬКО если тело job'ы
// прокидывает полученный ct в свои EF/IO-вызовы. Здесь фиксируем это свойство: вызов
// RunAsync с УЖЕ отменённым токеном завершается OperationCanceledException (а не молотит
// до конца), значит при shutdown джоба остановится кооперативно. Берём две
// репрезентативные идемпотентные retention-джобы (provider-portable на SQLite).
public sealed class JobCancellationContractTests
{
    private static readonly DateTime Now = new(2026, 6, 6, 3, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LicenseUsageRetentionJob_honors_a_cancelled_token()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(SettingKey.LicenseUsageRetentionDays).Returns(30);

        using (var seed = sqlite.NewContext())
        {
            seed.LicenseUsageSnapshots.Add(new LicenseUsageSnapshot
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                BucketStartUtc = Now.AddDays(-60),
                ConsumedMin = 0,
                ConsumedMax = 1,
                ConsumedAvg = 0.5,
                Limit = 10,
            });
            await seed.SaveChangesAsync();
        }

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var ctx = sqlite.NewContext();
        var job = new LicenseUsageRetentionJob(
            ctx, settings, TestHelpers.FixedClock(Now),
            NullLogger<LicenseUsageRetentionJob>.Instance);

        var act = async () => await job.RunAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "тело джобы прокидывает ct в EF-вызовы — при shutdown Hangfire подменит " +
            "плейсхолдер реальным токеном, и джоба остановится кооперативно");

        // Старая строка НЕ удалена — джоба не доработала до конца под отменой.
        using var verify = sqlite.NewContext();
        verify.LicenseUsageSnapshots.Count().Should().Be(1);
    }

    [Fact]
    public async Task AuditRetentionJob_honors_a_cancelled_token()
    {
        using var sqlite = TestHelpers.SqliteTestDb.Create();
        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(SettingKey.AuditRetentionDays).Returns(30);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var ctx = sqlite.NewContext();
        var job = new AuditRetentionJob(
            ctx, new TestHelpers.CapturingAuditLogger(), settings, TestHelpers.FixedClock(Now),
            NullLogger<AuditRetentionJob>.Instance);

        var act = async () => await job.RunAsync(cts.Token);

        // BeginTransactionAsync(ct) — первый ct-aware вызов в теле; под отменённым токеном
        // он бросает OperationCanceledException ещё до raw `DELETE TOP` (который на SQLite
        // не парсится). Любой из сценариев означает, что job не доходит до конца под отменой;
        // нам важно именно поведение отмены, поэтому ждём OperationCanceledException.
        await act.Should().ThrowAsync<OperationCanceledException>(
            "тело джобы передаёт ct в BeginTransactionAsync/ExecuteSql — shutdown кооперативен");
    }
}
