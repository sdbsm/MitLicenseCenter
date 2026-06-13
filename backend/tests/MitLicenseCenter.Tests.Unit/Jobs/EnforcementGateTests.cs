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
        // BE-24 (MLC-120): детерминированно, без Task.Delay. Каждый воркер, войдя в
        // критическую секцию, инкрементит счётчик «внутри», фиксирует максимум и
        // СИГНАЛИЗИРУЕТ тесту, что он внутри; затем блокируется на общем release-TCS.
        // Тест отпускает воркеров строго по одному и каждый раз проверяет, что внутри
        // ровно один путь. Окно перекрытия не зависит от таймингов — оно создаётся
        // явным сигналом «вошёл», а не sleep'ом.
        var gate = new EnforcementGate();
        var inside = 0;
        var maxObserved = 0;
        var maxLock = new object();

        var entered = new System.Collections.Concurrent.BlockingCollection<TaskCompletionSource>();

        async Task Worker()
        {
            using (await gate.AcquireAsync(CancellationToken.None))
            {
                var now = Interlocked.Increment(ref inside);
                lock (maxLock)
                    maxObserved = Math.Max(maxObserved, now);

                // Сообщаем тесту «я внутри» и ждём его команды на выход.
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                entered.Add(release);
                await release.Task;

                Interlocked.Decrement(ref inside);
            }
        }

        const int workers = 8;
        var all = Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(Worker)));

        for (var i = 0; i < workers; i++)
        {
            // Take блокируется, пока ровно следующий воркер не войдёт в секцию.
            var release = entered.Take();
            Volatile.Read(ref inside).Should().Be(1, "в критической секции enforcement всегда ровно 1 путь");
            release.SetResult(); // выпускаем текущего → освобождается место для следующего.
        }

        await all;
        maxObserved.Should().Be(1, "за всё время максимум одновременных входов — 1");
    }

    [Fact]
    public async Task Second_caller_blocks_until_first_releases()
    {
        // BE-24 (MLC-120): детерминированно, без Task.Delay. Второй вызывающий сигналит
        // «я начал ждать замок» ПЕРЕД AcquireAsync; раз первый держит семафор, задача
        // захвата логически НЕ может завершиться — проверяем это, а не «подождём 50мс».
        var gate = new EnforcementGate();
        var first = await gate.AcquireAsync(CancellationToken.None);

        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = Task.Run(async () =>
        {
            secondStarted.SetResult();
            using (await gate.AcquireAsync(CancellationToken.None))
            {
                // вошёл — секция занята вторым
            }
        });

        await secondStarted.Task; // второй точно дошёл до попытки захвата.
        second.IsCompleted.Should().BeFalse("пока первый держит замок, второй не входит");

        first.Dispose();
        await second; // после релиза первого второй детерминированно завершает захват.
        second.IsCompletedSuccessfully.Should().BeTrue("после релиза первого второй входит");
    }

    [Fact]
    public async Task Double_dispose_releases_only_once()
    {
        var gate = new EnforcementGate();

        var releaser = await gate.AcquireAsync(CancellationToken.None);
        releaser.Dispose();
        releaser.Dispose(); // повторный Dispose не должен «лишний раз» отпустить семафор

        // Если бы второй Dispose отпустил семафор повторно, вместимость стала бы 2 и
        // одновременно вошли бы двое. Проверяем детерминированно (без Task.Delay): пока
        // первый держатель `a` не отпустит замок, задача захвата `b` завершиться не может.
        using var a = await gate.AcquireAsync(CancellationToken.None);

        var bStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var b = Task.Run(async () =>
        {
            bStarted.SetResult();
            using (await gate.AcquireAsync(CancellationToken.None))
            {
                // вошёл
            }
        });

        await bStarted.Task;
        b.IsCompleted.Should().BeFalse("вместимость замка осталась 1 (double-dispose идемпотентен)");

        a.Dispose();
        await b; // после релиза `a` второй входит — значит вместимость ровно 1, не 2.
        b.IsCompletedSuccessfully.Should().BeTrue();
    }
}
