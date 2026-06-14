using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Jobs;
using NSubstitute;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Jobs;

// MLC-154: cold-цикл согласования перенесён из Hangfire в ColdTierPollingService
// (BackgroundService), чтобы настройка Polling.ColdIntervalSeconds реально соблюдалась
// (Hangfire-CRON minimum = 1 мин делал её инертной). Каданс самого таймера тут НЕ драйвим —
// проверяем детерминированный seam RunOnceAsync (как RunCycleOnceAsync у hot): один вызов
// создаёт scope, резолвит scoped IReconciliationJob (нужен DbContext) и прогоняет cold ровно раз.
public sealed class ColdTierPollingServiceTests
{
    [Fact]
    public async Task RunOnceAsync_resolves_scoped_job_and_runs_cold_exactly_once()
    {
        var job = Substitute.For<IReconciliationJob>();

        // Реальный DI-контейнер: IReconciliationJob зарегистрирован как scoped — сервис
        // ОБЯЗАН резолвить его из собственного scope (production: scoped DbContext).
        var services = new ServiceCollection();
        services.AddScoped(_ => job);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var settings = Substitute.For<ISettingsSnapshot>();

        var service = new ColdTierPollingService(
            scopeFactory, settings, NullLogger<ColdTierPollingService>.Instance);

        await service.RunOnceAsync(CancellationToken.None);

        await job.Received(1).RunColdAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunOnceAsync_propagates_the_cancellation_token_to_the_job()
    {
        var job = Substitute.For<IReconciliationJob>();

        var services = new ServiceCollection();
        services.AddScoped(_ => job);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var settings = Substitute.For<ISettingsSnapshot>();
        var service = new ColdTierPollingService(
            scopeFactory, settings, NullLogger<ColdTierPollingService>.Instance);

        using var cts = new CancellationTokenSource();
        await service.RunOnceAsync(cts.Token);

        await job.Received(1).RunColdAsync(cts.Token);
    }

    // MLC-156: RunNowAsync будит петлю ExecuteAsync → cold-прогон идёт немедленно, а
    // возвращённый Task завершается ровно по окончании этого прогона. Каданс таймера тут
    // не драйвим — интервал большой (warm-up прогон + ожидание сигнала), сигнал прерывает
    // ожидание.
    [Fact]
    public async Task RunNowAsync_triggers_a_cold_run_and_completes_when_it_finishes()
    {
        var runCount = 0;
        var job = Substitute.For<IReconciliationJob>();
        job.RunColdAsync(Arg.Any<CancellationToken>())
            .Returns(_ => { Interlocked.Increment(ref runCount); return Task.CompletedTask; });

        var services = new ServiceCollection();
        services.AddScoped(_ => job);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var settings = Substitute.For<ISettingsSnapshot>();
        // Большой интервал: тиков по таймеру не будет за время теста — прогон идёт только
        // по сигналу RunNowAsync (плюс один warm-up при старте).
        settings.GetInt(SettingKey.PollingColdIntervalSeconds).Returns(3600);

        var service = new ColdTierPollingService(
            scopeFactory, settings, NullLogger<ColdTierPollingService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            // Дождёмся warm-up прогона, чтобы петля точно вошла в ожидание сигнала.
            await WaitUntilAsync(() => runCount >= 1);
            var before = runCount;

            await service.RunNowAsync(CancellationToken.None);

            // Прогон состоялся именно к моменту завершения RunNowAsync.
            runCount.Should().BeGreaterThan(before);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // Несколько одновременных RunNowAsync схлопываются в один ближайший прогон
    // (коалесцирование _pendingRun): прогонов не больше, чем тиков петли.
    [Fact]
    public async Task Concurrent_RunNowAsync_calls_coalesce_into_a_single_run()
    {
        var runCount = 0;
        // Гейт: первый прогон зависает, пока тест не «отпустит», — чтобы успеть накопить
        // несколько параллельных RunNowAsync до того, как петля заберёт _pendingRun.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var job = Substitute.For<IReconciliationJob>();
        job.RunColdAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                var n = Interlocked.Increment(ref runCount);
                if (n == 1) // warm-up: освобождаем сразу, ждём только на live-прогонах
                    return;
                entered.TrySetResult();
                await release.Task;
            });

        var services = new ServiceCollection();
        services.AddScoped(_ => job);
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var settings = Substitute.For<ISettingsSnapshot>();
        settings.GetInt(SettingKey.PollingColdIntervalSeconds).Returns(3600);

        var service = new ColdTierPollingService(
            scopeFactory, settings, NullLogger<ColdTierPollingService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => runCount >= 1); // warm-up завершился

            // Первый RunNowAsync стартует live-прогон, который зависнет внутри job.
            var first = service.RunNowAsync(CancellationToken.None);
            await entered.Task; // петля вошла в live-прогон и держит его

            // Пока прогон висит, накапливаем ещё запросы — они должны коалесцироваться
            // в ОДИН следующий прогон.
            var second = service.RunNowAsync(CancellationToken.None);
            var third = service.RunNowAsync(CancellationToken.None);

            release.SetResult(); // отпускаем; дальше пойдёт ровно один коалесцированный прогон
            await Task.WhenAll(first, second, third).WaitAsync(TimeSpan.FromSeconds(10));

            // 1 warm-up + 1 live (first) + 1 коалесцированный (second+third) = 3 прогона.
            // Главное — не 4: second и third не дали отдельных прогонов.
            runCount.Should().Be(3);
        }
        finally
        {
            release.TrySetResult();
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition not met within timeout.");
            await Task.Delay(10);
        }
    }
}
