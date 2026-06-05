using FluentAssertions;
using MitLicenseCenter.Infrastructure.Publishing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Publishing;

// MLC-046: ограничитель одновременных спавнов webinst обязан держать число
// параллельных операций в пределах MaxConcurrency — иначе пачка из ~100 публикаций
// положит сервер роем процессов (семья ADR-3.3).
public sealed class WebinstConcurrencyGateTests
{
    [Fact]
    public async Task Acquire_caps_concurrency_at_max()
    {
        var gate = new WebinstConcurrencyGate();
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

        await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => Task.Run(Worker)));

        maxObserved.Should().BeLessThanOrEqualTo(
            WebinstConcurrencyGate.MaxConcurrency,
            "одновременно работающих webinst не больше кэпа");
        maxObserved.Should().Be(
            WebinstConcurrencyGate.MaxConcurrency,
            "под нагрузкой кэп должен полностью утилизироваться (нет искусственной сериализации)");
    }

    [Fact]
    public async Task Acquire_beyond_capacity_blocks_until_a_slot_frees()
    {
        var gate = new WebinstConcurrencyGate();

        // Занимаем все слоты.
        var held = new List<IDisposable>();
        for (var i = 0; i < WebinstConcurrencyGate.MaxConcurrency; i++)
            held.Add(await gate.AcquireAsync(CancellationToken.None));

        var extraEntered = false;
        var extra = Task.Run(async () =>
        {
            using (await gate.AcquireAsync(CancellationToken.None))
                Volatile.Write(ref extraEntered, true);
        });

        await Task.Delay(50);
        Volatile.Read(ref extraEntered).Should().BeFalse("пока все слоты заняты, лишний ждёт");

        held[0].Dispose();
        await extra;
        Volatile.Read(ref extraEntered).Should().BeTrue("после освобождения слота лишний входит");

        for (var i = 1; i < held.Count; i++)
            held[i].Dispose();
    }

    [Fact]
    public async Task Double_dispose_releases_only_one_slot()
    {
        var gate = new WebinstConcurrencyGate();

        var releaser = await gate.AcquireAsync(CancellationToken.None);
        releaser.Dispose();
        releaser.Dispose(); // повторный Dispose не должен «вернуть» лишний слот

        // Занимаем все слоты; если double-dispose добавил лишний — вместимость превысила бы кэп.
        var held = new List<IDisposable>();
        for (var i = 0; i < WebinstConcurrencyGate.MaxConcurrency; i++)
            held.Add(await gate.AcquireAsync(CancellationToken.None));

        var extraEntered = false;
        var extra = Task.Run(async () =>
        {
            using (await gate.AcquireAsync(CancellationToken.None))
                Volatile.Write(ref extraEntered, true);
        });

        await Task.Delay(50);
        Volatile.Read(ref extraEntered).Should().BeFalse("вместимость осталась MaxConcurrency (double-dispose идемпотентен)");

        held[0].Dispose();
        await extra;
        for (var i = 1; i < held.Count; i++)
            held[i].Dispose();
    }
}
