using System.Diagnostics.Metrics;
using FluentAssertions;
using MitLicenseCenter.Infrastructure.Diagnostics;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Diagnostics;

// MLC-037 (PERF-01): доказывает, что инструменты rac-метрик реально публикуются и
// несут ожидаемые теги при активном слушателе (как это делает dotnet-counters).
// Реальный rac.exe не запускается — проверяется только поверхность метрик.
public sealed class RacMetricsTests
{
    [Fact]
    public void Record_increments_spawn_counter_and_writes_duration_with_tags()
    {
        using var metrics = TestMetrics.Rac();

        long spawnTotal = 0;
        var spawnTags = new List<KeyValuePair<string, object?>>();
        double durationValue = 0;
        var durationTags = new List<KeyValuePair<string, object?>>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instr, l) =>
            {
                if (instr.Meter.Name == RacMetrics.MeterName)
                    l.EnableMeasurementEvents(instr);
            },
        };
        listener.SetMeasurementEventCallback<long>((instr, measurement, tags, _) =>
        {
            if (instr.Name == "rac.exe.spawns")
            {
                spawnTotal += measurement;
                spawnTags.AddRange(tags.ToArray());
            }
        });
        listener.SetMeasurementEventCallback<double>((instr, measurement, tags, _) =>
        {
            if (instr.Name == "rac.exe.invocation.duration")
            {
                durationValue = measurement;
                durationTags.AddRange(tags.ToArray());
            }
        });
        listener.Start();

        // С активным слушателем гард Enabled должен пропустить запись.
        metrics.Enabled.Should().BeTrue();

        metrics.Record(RacCommandTag.SessionList, 12.5, RacMetrics.OutcomeOk);

        spawnTotal.Should().Be(1);
        spawnTags.Should().ContainSingle(t => t.Key == "command")
            .Which.Value.Should().Be(RacCommandTag.SessionList);

        durationValue.Should().Be(12.5);
        durationTags.Should().Contain(t => t.Key == "command" && Equals(t.Value, RacCommandTag.SessionList));
        durationTags.Should().Contain(t => t.Key == "outcome" && Equals(t.Value, RacMetrics.OutcomeOk));
    }

    [Fact]
    public void Disabled_without_listener()
    {
        using var metrics = TestMetrics.Rac();
        metrics.Enabled.Should().BeFalse("без активного слушателя инструменты выключены — near-zero overhead");
    }
}
