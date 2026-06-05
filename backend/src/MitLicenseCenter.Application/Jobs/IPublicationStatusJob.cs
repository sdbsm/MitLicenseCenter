namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job (MLC-045): read-only обновление статуса публикаций в IIS — читает
// факт (есть ли сайт/vdir/web.config, версия платформы) и пишет LastCheck*.
// НЕ enforcement: сравнения с эталоном и авто-исправления нет, аудит не пишется.
// Recurring instance тикает раз в минуту (внутренний throttle до
// Settings.Drift.IntervalMinutes), on-demand — через POST /publications/{id}/check.
public interface IPublicationStatusJob
{
    Task RefreshAllAsync(CancellationToken ct);
    Task RefreshOneAsync(Guid publicationId, CancellationToken ct);
}
