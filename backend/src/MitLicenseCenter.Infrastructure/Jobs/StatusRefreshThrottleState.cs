namespace MitLicenseCenter.Infrastructure.Jobs;

// Singleton throttle для PublicationStatusRefreshJob.RefreshAllAsync (MLC-045).
// Hangfire тикает recurring-job каждую минуту ("*/5 * * * *" в Program.cs), а
// оператор может задать Settings.Drift.IntervalMinutes = 30 — без этого state'а
// мы бы бегали поверх плана. Mirror'им паттерн ColdThrottleState из PR 3.3.
//
// Throttle действует ТОЛЬКО на recurring RefreshAllAsync. On-demand RefreshOneAsync
// (вызываемый через POST /publications/{id}/check) не throttle'ится — оператор
// имеет право проверить «сейчас».
internal sealed class StatusRefreshThrottleState
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
