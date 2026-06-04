using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using MitLicenseCenter.Application.Sessions;
using MitLicenseCenter.Infrastructure.Diagnostics;
using MitLicenseCenter.Infrastructure.Jobs;

namespace MitLicenseCenter.Tests.Unit.Diagnostics;

// MLC-037 (PERF-01): метрики горячего пути для unit-тестов. Реального слушателя нет —
// инструменты no-op, но конструкторы инструментированных типов требуют экземпляры.
// IMeterFactory берём из минимального DI-контейнера.
internal static class TestMetrics
{
    public static IMeterFactory MeterFactory()
        => new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider()
            .GetRequiredService<IMeterFactory>();

    public static RacMetrics Rac() => new(MeterFactory());

    public static ReconciliationMetrics Reconciliation(IHotTierRegistry? registry = null)
        => new(MeterFactory(), registry ?? new HotTierRegistry());
}
