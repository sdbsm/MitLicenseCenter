namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job: проверка дрейфа default.vrd относительно desired-state публикаций.
// Recurring instance запускается раз в минуту (внутренний throttle до
// Settings.Drift.IntervalMinutes), on-demand instance — через /check-drift endpoint.
public interface IDriftCheckJob
{
    Task RunAllAsync(CancellationToken ct);
    Task CheckOneAsync(Guid publicationId, CancellationToken ct);
}
