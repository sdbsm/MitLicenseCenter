namespace MitLicenseCenter.Infrastructure.Jobs;

// Singleton throttle для DriftCheckJob.RunAllAsync. Hangfire тикает recurring-job
// каждую минуту ("*/5 * * * *" в Program.cs), а оператор может задать
// Settings.Drift.IntervalMinutes = 30 — без этого state'а мы бы бегали поверх
// плана. Mirror'им паттерн ColdThrottleState из PR 3.3.
//
// Throttle действует ТОЛЬКО на recurring RunAllAsync. On-demand CheckOneAsync
// (вызываемый через /check-drift endpoint и из reconcile-endpoint'а) не
// throttle'ится — оператор имеет право проверить «сейчас».
internal sealed class DriftThrottleState
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
