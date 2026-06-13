using Hangfire;

namespace MitLicenseCenter.Application.Jobs;

// Hangfire-job (MLC-045): read-only обновление статуса публикаций в IIS — читает
// факт (есть ли сайт/vdir/web.config, версия платформы) и пишет LastCheck*.
// НЕ enforcement: сравнения с эталоном и авто-исправления нет, аудит не пишется.
// Recurring instance тикает раз в минуту (внутренний throttle до
// Settings.Drift.IntervalMinutes), on-demand — через POST /publications/{id}/check.
public interface IPublicationStatusJob
{
    // Read-only тик каждые 5 мин, самоисцеляется на следующем тике (MLC-123, REL-22):
    // упавший тик безвреден, ретраи лишь копят красный шум. Attempts=0 — без ретраев,
    // ждём следующий плановый тик. Атрибут на методе интерфейса
    // (RecurringJob.AddOrUpdate<IPublicationStatusJob>) — Hangfire берёт фильтры с него.
    [AutomaticRetry(Attempts = 0)]
    Task RefreshAllAsync(CancellationToken ct);
    Task RefreshOneAsync(Guid publicationId, CancellationToken ct);
}
