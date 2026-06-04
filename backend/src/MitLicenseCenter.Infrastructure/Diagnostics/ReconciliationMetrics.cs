using System.Diagnostics.Metrics;
using MitLicenseCenter.Application.Sessions;

namespace MitLicenseCenter.Infrastructure.Diagnostics;

// MLC-037 (PERF-01): метрики цикла согласования — длительность cold/hot-цикла, число
// kills за цикл и текущее число hot-тенантов. Гистограммы переиспользуют существующие
// Stopwatch'и джобов (ReconciliationJob / HotTierPollingService), ничего не добавляя в
// горячий путь. hot_tenants — ObservableGauge: значение читается из IHotTierRegistry на
// сборке метрики слушателем (dotnet-counters), а не на горячем пути.
// Регистрируется singleton'ом в Infrastructure.DependencyInjection. ADR-15: только
// System.Diagnostics.Metrics, без внешних систем.
internal sealed class ReconciliationMetrics : IDisposable
{
    public const string MeterName = "MitLicenseCenter.Reconciliation";

    private readonly Meter _meter;
    private readonly Histogram<double> _coldDuration;
    private readonly Histogram<double> _hotDuration;
    private readonly Counter<long> _kills;

    public ReconciliationMetrics(IMeterFactory meterFactory, IHotTierRegistry hotTier)
    {
        _meter = meterFactory.Create(MeterName);

        _coldDuration = _meter.CreateHistogram<double>(
            "reconciliation.cold.duration",
            unit: "ms",
            description: "Длительность успешного cold-цикла согласования.");

        _hotDuration = _meter.CreateHistogram<double>(
            "reconciliation.hot.duration",
            unit: "ms",
            description: "Длительность hot-поллинга (RAS-fetch активных сессий).");

        _kills = _meter.CreateCounter<long>(
            "reconciliation.kills",
            unit: "{session}",
            description: "Число завершённых сессий (kill) за цикл enforcement.");

        _meter.CreateObservableGauge(
            "reconciliation.hot_tenants",
            () => hotTier.CurrentHotTenants().Count,
            unit: "{tenant}",
            description: "Текущее число hot-тенантов в реестре.");
    }

    public void RecordColdCycle(double elapsedMs) => _coldDuration.Record(elapsedMs);

    public void RecordHotCycle(double elapsedMs) => _hotDuration.Record(elapsedMs);

    public void AddKills(int count) => _kills.Add(count);

    public void Dispose() => _meter.Dispose();
}
