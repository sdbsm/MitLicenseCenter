using System.Reflection;
using FluentAssertions;
using Hangfire;
using MitLicenseCenter.Application.Jobs;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-123 (BE-21): осознанная retry-политика на идемпотентных recurring-джобах. Без явного
// [AutomaticRetry] Hangfire даёт дефолтные 10 ретраев — лишний красный шум при стойком сбое.
// Атрибут обязан жить на методе ИНТЕРФЕЙСА: job'ы зарегистрированы через интерфейс
// (RecurringJob.AddOrUpdate<TInterface>), и Hangfire берёт серверные фильтры именно с
// зарегистрированного метода (тот же инвариант, что у DisableConcurrentExecution в
// ReconciliationJobConcurrencyGuardTests).
public sealed class JobRetryPolicyTests
{
    [Theory]
    [InlineData(typeof(IAuditRetentionJob), nameof(IAuditRetentionJob.RunAsync), 3)]
    [InlineData(typeof(ILicenseUsageRetentionJob), nameof(ILicenseUsageRetentionJob.RunAsync), 3)]
    [InlineData(typeof(IBackupRetentionJob), nameof(IBackupRetentionJob.RunAsync), 3)]
    public void Daily_housekeeping_jobs_retry_a_few_times_then_fail(
        Type jobInterface, string method, int expectedAttempts)
    {
        var attribute = GetRetryAttribute(jobInterface, method);

        attribute.Should().NotBeNull(
            "идемпотентные ночные джобы должны иметь осознанную retry-политику, а не дефолтные 10");
        attribute!.Attempts.Should().Be(expectedAttempts);
        attribute.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail,
            "после исчерпания попыток джоба остаётся видимо-упавшей; самоисцеляется следующий запуск");
    }

    [Theory]
    [InlineData(typeof(IPublicationStatusJob), nameof(IPublicationStatusJob.RefreshAllAsync))]
    [InlineData(typeof(IReconciliationJob), nameof(IReconciliationJob.RunColdAsync))]
    public void Self_healing_high_frequency_jobs_do_not_retry(Type jobInterface, string method)
    {
        var attribute = GetRetryAttribute(jobInterface, method);

        attribute.Should().NotBeNull("явная политика «без ретраев» лучше дефолтных 10");
        attribute!.Attempts.Should().Be(0,
            "частый самоисцеляющийся тик не нуждается в ретраях — следующий тик восстановит");
    }

    private static AutomaticRetryAttribute? GetRetryAttribute(Type jobInterface, string method) =>
        jobInterface.GetMethod(method)!.GetCustomAttribute<AutomaticRetryAttribute>();
}
