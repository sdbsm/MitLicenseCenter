namespace MitLicenseCenter.Infrastructure.Jobs;

internal sealed class ColdThrottleState
{
    private readonly object _gate = new();
    private DateTime _lastRunAttemptUtc = DateTime.MinValue;

    public DateTime LastRunAttemptUtc
    {
        get { lock (_gate) return _lastRunAttemptUtc; }
    }

    public void MarkRun(DateTime utcNow)
    {
        lock (_gate) _lastRunAttemptUtc = utcNow;
    }
}
