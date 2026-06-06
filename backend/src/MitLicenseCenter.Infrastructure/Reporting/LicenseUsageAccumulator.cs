using MitLicenseCenter.Application.Reporting;

namespace MitLicenseCenter.Infrastructure.Reporting;

// Singleton-аккумулятор сбора time-series потребления (MLC-048, ADR-25). Cold-джоба
// (scoped) подаёт семпл каждые ≈25с; состояние текущего 15-мин бакета живёт здесь, между
// инвокациями. На пересечении границы бакета возвращает агрегаты прошлого бакета и
// начинает новый. Thread-safe через lock — hot/cold-пути джоб резолвятся в разных scope'ах,
// но по одному singleton'у. Частичный бакет при рестарте теряется (best-effort).
internal sealed class LicenseUsageAccumulator : ILicenseUsageAccumulator
{
    private static readonly long BucketTicks = TimeSpan.FromMinutes(15).Ticks;

    private readonly object _gate = new();
    private readonly Dictionary<Guid, TenantRunning> _running = new();
    private DateTime? _currentBucketStart;

    public IReadOnlyList<LicenseUsageBucket> RecordSample(
        DateTime sampleUtc,
        IReadOnlyCollection<LicenseUsageSample> samples)
    {
        var bucketStart = FloorToBucket(sampleUtc);

        lock (_gate)
        {
            IReadOnlyList<LicenseUsageBucket> flushed = [];

            if (_currentBucketStart is { } current)
            {
                // Часы откатились назад (NTP/перевод) — игнорируем семпл, не ломаем бакет.
                if (bucketStart < current)
                    return flushed;

                // Пересекли границу — закрываем прошлый бакет и стартуем новый.
                if (bucketStart > current)
                {
                    flushed = FlushLocked(current);
                    _running.Clear();
                }
            }

            _currentBucketStart = bucketStart;
            FoldLocked(samples);

            return flushed;
        }
    }

    private void FoldLocked(IReadOnlyCollection<LicenseUsageSample> samples)
    {
        foreach (var s in samples)
        {
            if (_running.TryGetValue(s.TenantId, out var r))
            {
                r.Min = Math.Min(r.Min, s.Consumed);
                r.Max = Math.Max(r.Max, s.Consumed);
                r.Sum += s.Consumed;
                r.Count++;
                r.LastLimit = s.Limit; // последний наблюдённый лимит в бакете
            }
            else
            {
                _running[s.TenantId] = new TenantRunning
                {
                    Min = s.Consumed,
                    Max = s.Consumed,
                    Sum = s.Consumed,
                    Count = 1,
                    LastLimit = s.Limit,
                };
            }
        }
    }

    private List<LicenseUsageBucket> FlushLocked(DateTime bucketStart)
    {
        var rows = new List<LicenseUsageBucket>(_running.Count);
        foreach (var (tenantId, r) in _running)
        {
            rows.Add(new LicenseUsageBucket(
                tenantId,
                bucketStart,
                r.Min,
                r.Max,
                (double)r.Sum / r.Count,
                r.LastLimit));
        }

        return rows;
    }

    private static DateTime FloorToBucket(DateTime sampleUtc) =>
        new(sampleUtc.Ticks / BucketTicks * BucketTicks, DateTimeKind.Utc);

    private sealed class TenantRunning
    {
        public int Min;
        public int Max;
        public long Sum;
        public int Count;
        public int LastLimit;
    }
}
