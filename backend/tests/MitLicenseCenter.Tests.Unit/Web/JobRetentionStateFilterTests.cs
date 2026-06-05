using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using MitLicenseCenter.Web.Hangfire;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Web;

// JobRetentionStateFilter держит схему hangfire ограниченной: завершённые джобы
// истекают через 2 дня, упавшие — остаются для разбора (фильтр их не трогает).
public sealed class JobRetentionStateFilterTests
{
    private static readonly TimeSpan ExpectedFinishedRetention = TimeSpan.FromDays(2);

    [Fact]
    public void Succeeded_jobs_expire_after_two_days()
    {
        var context = MakeContext(new SucceededState(result: null, latency: 0, performanceDuration: 0));

        new JobRetentionStateFilter().OnStateApplied(context, Substitute.For<IWriteOnlyTransaction>());

        context.JobExpirationTimeout.Should().Be(ExpectedFinishedRetention);
    }

    [Fact]
    public void Deleted_jobs_expire_after_two_days()
    {
        var context = MakeContext(new DeletedState());

        new JobRetentionStateFilter().OnStateApplied(context, Substitute.For<IWriteOnlyTransaction>());

        context.JobExpirationTimeout.Should().Be(ExpectedFinishedRetention);
    }

    [Fact]
    public void Failed_jobs_keep_the_default_retention()
    {
        var context = MakeContext(new FailedState(new InvalidOperationException("boom")));
        var defaultTimeout = context.JobExpirationTimeout;

        new JobRetentionStateFilter().OnStateApplied(context, Substitute.For<IWriteOnlyTransaction>());

        // Фильтр не вмешивается в failed-состояние — таймаут остаётся дефолтным
        // (и не равен нашему окну для завершённых джоб).
        context.JobExpirationTimeout.Should().Be(defaultTimeout);
        context.JobExpirationTimeout.Should().NotBe(ExpectedFinishedRetention);
    }

    private static ApplyStateContext MakeContext(IState newState)
    {
        var storage = Substitute.For<Hangfire.JobStorage>();
        var connection = Substitute.For<IStorageConnection>();
        var transaction = Substitute.For<IWriteOnlyTransaction>();
        var job = Job.FromExpression(() => Console.WriteLine());
        var backgroundJob = new BackgroundJob(
            "job-1",
            job,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        return new ApplyStateContext(
            storage,
            connection,
            transaction,
            backgroundJob,
            newState,
            oldStateName: "Processing");
    }
}
