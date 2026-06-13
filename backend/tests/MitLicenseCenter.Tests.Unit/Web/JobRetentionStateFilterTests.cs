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
// истекают через 2 дня, упавшие — через 30 дней (видимость для разбора + самоочистка,
// MLC-123/REL-22).
public sealed class JobRetentionStateFilterTests
{
    private static readonly TimeSpan ExpectedFinishedRetention = TimeSpan.FromDays(2);
    private static readonly TimeSpan ExpectedFailedRetention = TimeSpan.FromDays(30);

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
    public void Failed_jobs_expire_after_thirty_days()
    {
        var context = MakeContext(new FailedState(new InvalidOperationException("boom")));

        new JobRetentionStateFilter().OnStateApplied(context, Substitute.For<IWriteOnlyTransaction>());

        // MLC-123/REL-22: упавшие джобы теперь получают собственное, более долгое окно —
        // 30 дней (видимость для разбора), но не копятся вечно. И это не окно завершённых.
        context.JobExpirationTimeout.Should().Be(ExpectedFailedRetention);
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
