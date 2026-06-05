using FluentAssertions;
using MitLicenseCenter.Infrastructure.Jobs;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-044: общий enforcement-замок обязан гарантировать взаимоисключение — в любой
// момент в критической секции находится ровно один путь (cold ИЛИ hot), иначе
// возможен over-kill (MLC-001).
public sealed class EnforcementGateTests
{
    [Fact]
    public async Task Acquire_serializes_concurrent_callers()
    {
        var gate = new EnforcementGate();
        var inside = 0;
        var maxObserved = 0;
        var maxLock = new object();

        async Task Worker()
        {
            using (await gate.AcquireAsync(CancellationToken.None))
            {
                var now = Interlocked.Increment(ref inside);
                lock (maxLock)
                    maxObserved = Math.Max(maxObserved, now);
                await Task.Delay(15);
                Interlocked.Decrement(ref inside);
            }
        }

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(Worker)));

        maxObserved.Should().Be(1, "в критической секции enforcement всегда ≤1 путь");
    }

    [Fact]
    public async Task Second_caller_blocks_until_first_releases()
    {
        var gate = new EnforcementGate();
        var first = await gate.AcquireAsync(CancellationToken.None);

        var secondEntered = false;
        var second = Task.Run(async () =>
        {
            using (await gate.AcquireAsync(CancellationToken.None))
                Volatile.Write(ref secondEntered, true);
        });

        await Task.Delay(50);
        Volatile.Read(ref secondEntered).Should().BeFalse("пока первый держит замок, второй не входит");

        first.Dispose();
        await second;
        Volatile.Read(ref secondEntered).Should().BeTrue("после релиза первого второй входит");
    }

    [Fact]
    public async Task Double_dispose_releases_only_once()
    {
        var gate = new EnforcementGate();

        var releaser = await gate.AcquireAsync(CancellationToken.None);
        releaser.Dispose();
        releaser.Dispose(); // повторный Dispose не должен «лишний раз» отпустить семафор

        // Если бы второй Dispose отпустил семафор повторно, счётчик стал бы 2 и
        // одновременно вошли бы двое. Проверяем, что вместимость осталась 1.
        using var a = await gate.AcquireAsync(CancellationToken.None);
        var bEntered = false;
        var b = Task.Run(async () =>
        {
            using (await gate.AcquireAsync(CancellationToken.None))
                Volatile.Write(ref bEntered, true);
        });

        await Task.Delay(50);
        Volatile.Read(ref bEntered).Should().BeFalse("вместимость замка осталась 1 (double-dispose идемпотентен)");

        a.Dispose();
        await b;
    }
}
