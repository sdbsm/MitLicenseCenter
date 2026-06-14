using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Settings;
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
}
