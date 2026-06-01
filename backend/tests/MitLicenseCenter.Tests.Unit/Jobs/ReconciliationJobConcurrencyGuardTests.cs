using System.Reflection;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using MitLicenseCenter.Application.Jobs;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-001: цикл согласования не должен исполняться параллельно сам с собой, иначе два
// цикла независимо считают over-limit и убивают суммарно больше сеансов, чем нужно.
public sealed class ReconciliationJobConcurrencyGuardTests
{
    private static readonly MethodInfo RunColdMethod =
        typeof(IReconciliationJob).GetMethod(nameof(IReconciliationJob.RunColdAsync))!;

    [Fact]
    public void RunColdAsync_is_marked_DisableConcurrentExecution()
    {
        // Атрибут обязан жить на методе интерфейса: job зарегистрирован через интерфейс
        // (RecurringJob.AddOrUpdate<IReconciliationJob>), и Hangfire берёт серверные
        // фильтры именно с зарегистрированного метода.
        var attribute = RunColdMethod.GetCustomAttribute<DisableConcurrentExecutionAttribute>();

        attribute.Should().NotBeNull(
            "иначе Hangfire допустит overlap минутных тиков cold-snapshot и возможен over-kill");
    }

    [Fact]
    public void Reentrant_invocation_is_blocked_while_a_cycle_holds_the_lock()
    {
        // Берём НАСТОЯЩИЙ сконфигурированный фильтр с метода интерфейса.
        var filter = RunColdMethod.GetCustomAttribute<DisableConcurrentExecutionAttribute>()!;

        // Stateful-заглушка соединения: моделирует единственный распределённый лок.
        // Пока лок удержан, повторный AcquireDistributedLock бросает таймаут — ровно так
        // Hangfire ведёт себя при overlap, и так не даёт телу job'ы (EnforceAsync) пойти второй раз.
        var held = false;
        var connection = Substitute.For<IStorageConnection>();
        connection.AcquireDistributedLock(Arg.Any<string>(), Arg.Any<TimeSpan>())
            .Returns(callInfo =>
            {
                if (held)
                    throw new DistributedLockTimeoutException((string)callInfo[0]);

                held = true;
                return new ReleaseOnDispose(() => held = false);
            });

        // Счётчик «запусков EnforceAsync»: тело job'ы исполняется только если фильтр
        // пропустил (OnPerforming не бросил).
        var enforceAsyncRuns = 0;

        void RunGuardedCycle()
        {
            var performing = NewPerformingContext(connection);
            filter.OnPerforming(performing);   // acquire lock — бросит, если уже занят
            enforceAsyncRuns++;                // суррогат EnforceAsync
        }

        // Первый цикл проходит и держит лок (Dispose не вызван — цикл «ещё идёт»).
        RunGuardedCycle();
        enforceAsyncRuns.Should().Be(1);

        // Re-entrant тик при занятом цикле: фильтр обязан заблокировать второй запуск.
        var reentrant = () => RunGuardedCycle();

        reentrant.Should().Throw<DistributedLockTimeoutException>();
        enforceAsyncRuns.Should().Be(1, "вторая EnforceAsync не должна запуститься параллельно");
    }

    private static PerformingContext NewPerformingContext(IStorageConnection connection)
    {
        var job = Job.FromExpression<IReconciliationJob>(j => j.RunColdAsync(CancellationToken.None));
        var backgroundJob = new BackgroundJob(Guid.NewGuid().ToString(), job, DateTime.UtcNow);
        var performContext = new PerformContext(
            storage: null,
            connection: connection,
            backgroundJob: backgroundJob,
            cancellationToken: new JobCancellationToken(canceled: false));

        return new PerformingContext(performContext);
    }

    private sealed class ReleaseOnDispose(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
