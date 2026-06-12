using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Publishing;

namespace MitLicenseCenter.Infrastructure.Jobs;

// Hangfire-job (MLC-045): read-only обновление статуса публикаций в IIS. Читает
// факт (ReadActualStateAsync) → оценивает PublicationStatusEvaluator → пишет
// LastCheck*. **Никогда не меняет IIS и не пишет аудит** — это не enforcement
// (ADR-4 переписан, ADR-4.1 revoked). Создание/смена платформы — отдельные
// явные действия оператора (webinst / change-platform endpoints).
internal sealed partial class PublicationStatusRefreshJob : IPublicationStatusJob
{
    private readonly AppDbContext _db;
    private readonly IIisPublishingService _iis;
    private readonly ISettingsSnapshot _settings;
    private readonly StatusRefreshThrottleState _throttle;
    private readonly TimeProvider _clock;
    private readonly ILogger<PublicationStatusRefreshJob> _logger;

    public PublicationStatusRefreshJob(
        AppDbContext db,
        IIisPublishingService iis,
        ISettingsSnapshot settings,
        StatusRefreshThrottleState throttle,
        TimeProvider clock,
        ILogger<PublicationStatusRefreshJob> logger)
    {
        _db = db;
        _iis = iis;
        _settings = settings;
        _throttle = throttle;
        _clock = clock;
        _logger = logger;
    }

    public async Task RefreshAllAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var intervalMinutes = _settings.GetInt(SettingKey.DriftIntervalMinutes) ?? 5;

        if ((now - _throttle.LastRunAttemptUtc).TotalMinutes < intervalMinutes)
            return;

        _throttle.MarkRun(now);

        try
        {
            // Один проекционный запрос нужных проверке полей всех публикаций
            // (PERF-07-паттерн): AsNoTracking, без tracking-снимков на весь объём.
            var snapshots = await LoadSnapshotsAsync(p => true, ct).ConfigureAwait(false);

            foreach (var snapshot in snapshots)
            {
                ct.ThrowIfCancellationRequested();

                // Per-item изоляция (BE-05): сбой одной публикации (напр.
                // DbUpdateConcurrencyException при параллельном удалении) не должен
                // прерывать обновление остальных. Отмену не глотаем — она прокидывается выше.
                try
                {
                    await ProcessOneAsync(snapshot, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogStatusItemFailed(_logger, snapshot.Id, ex);

                    // Снимаем со слежения «отравленный» attach неуспешного SaveChanges
                    // (RefreshAllAsync грузит снапшоты AsNoTracking → ProcessOne всегда
                    // Attach'ит транзиентный probe). Иначе он всплыл бы при сохранении
                    // следующего элемента. Detached, а не Modified/Added — только отвязка.
                    var stuck = _db.Publications.Local.FirstOrDefault(p => p.Id == snapshot.Id);
                    if (stuck is not null && _db.Entry(stuck).State != EntityState.Unchanged)
                        _db.Entry(stuck).State = EntityState.Detached;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogStatusRunFailed(_logger, ex);
        }
    }

    public async Task RefreshOneAsync(Guid publicationId, CancellationToken ct)
    {
        var snapshots = await LoadSnapshotsAsync(p => p.Id == publicationId, ct).ConfigureAwait(false);
        if (snapshots.Count == 0)
            return;
        await ProcessOneAsync(snapshots[0], ct).ConfigureAwait(false);
    }

    private async Task<List<StatusSnapshot>> LoadSnapshotsAsync(
        Expression<Func<Publication, bool>> filter,
        CancellationToken ct) =>
        await _db.Publications
            .AsNoTracking()
            .Where(filter)
            .Select(p => new StatusSnapshot(
                p.Id,
                p.SiteName,
                p.VirtualPath,
                p.PlatformVersion,
                p.PhysicalPathOverride))
            .ToListAsync(ct)
            .ConfigureAwait(false);

    private async Task ProcessOneAsync(StatusSnapshot snapshot, CancellationToken ct)
    {
        var probe = new Publication
        {
            Id = snapshot.Id,
            SiteName = snapshot.SiteName,
            VirtualPath = snapshot.VirtualPath,
            PlatformVersion = snapshot.PlatformVersion,
            PhysicalPathOverride = snapshot.PhysicalPathOverride,
        };

        var actual = await _iis.ReadActualStateAsync(probe, ct).ConfigureAwait(false);
        var (status, details) = PublicationStatusEvaluator.Evaluate(probe, actual);

        // Targeted-апдейт только статус-колонок по Id. Если сущность уже трекается
        // в текущем scope (напр. вызов из эндпоинта в том же DbContext) — мутируем её,
        // иначе attach'им транзиент и помечаем Modified только нужные поля.
        var tracked = _db.Publications.Local.FirstOrDefault(p => p.Id == snapshot.Id);
        var target = tracked ?? probe;
        if (tracked is null)
            _db.Attach(probe);

        target.LastCheckStatus = status;
        target.LastCheckAt = _clock.GetUtcNow().UtcDateTime;
        target.LastCheckDetails = string.IsNullOrEmpty(details) ? null : details;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private sealed record StatusSnapshot(
        Guid Id,
        string SiteName,
        string VirtualPath,
        string PlatformVersion,
        string? PhysicalPathOverride);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publication status refresh cycle failed")]
    private static partial void LogStatusRunFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publication status refresh failed for publication {PublicationId}; continuing with the rest")]
    private static partial void LogStatusItemFailed(ILogger logger, Guid publicationId, Exception ex);
}
