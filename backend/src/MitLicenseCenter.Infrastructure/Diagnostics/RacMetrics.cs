using System.Diagnostics.Metrics;

namespace MitLicenseCenter.Infrastructure.Diagnostics;

// MLC-037 (PERF-01): метрики единственной точки спавна rac.exe (SystemProcessRacRunner).
// Meter без активного слушателя — near-zero overhead; вызывающий код гардит запись через
// свойство Enabled, чтобы не вычислять тег команды и не аллоцировать на горячем пути в
// проде. Снимается через dotnet-counters по имени Meter (см. OPERATIONS.md «Наблюдаемость
// перфа»). Никаких внешних телеметрических систем — только System.Diagnostics.Metrics
// (ADR-15). Регистрируется singleton'ом в Infrastructure.DependencyInjection.
internal sealed class RacMetrics : IDisposable
{
    public const string MeterName = "MitLicenseCenter.Rac";

    public const string OutcomeOk = "ok";
    public const string OutcomeFailed = "failed";
    public const string OutcomeTimeout = "timeout";

    private readonly Meter _meter;
    private readonly Counter<long> _spawns;
    private readonly Histogram<double> _duration;

    public RacMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _spawns = _meter.CreateCounter<long>(
            "rac.exe.spawns",
            unit: "{spawn}",
            description: "Число спавнов процесса rac.exe (тег command).");

        _duration = _meter.CreateHistogram<double>(
            "rac.exe.invocation.duration",
            unit: "ms",
            description: "Длительность одного вызова rac.exe (теги command, outcome).");
    }

    // True, когда хотя бы один инструмент имеет активного слушателя. Используется
    // вызывающим кодом как гард: при выключенных метриках тег команды не вычисляется.
    public bool Enabled => _spawns.Enabled || _duration.Enabled;

    public void Record(string command, double elapsedMs, string outcome)
    {
        var commandTag = new KeyValuePair<string, object?>("command", command);
        _spawns.Add(1, commandTag);
        _duration.Record(
            elapsedMs,
            commandTag,
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public void Dispose() => _meter.Dispose();
}
