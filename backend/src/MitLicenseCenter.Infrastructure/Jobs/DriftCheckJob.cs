using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Publishing;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Publications;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Publishing;

namespace MitLicenseCenter.Infrastructure.Jobs;

// Hangfire-job (PR 3.5): сравнение Publication (desired) с PublicationActualState
// (IIS + default.vrd) через PublicationDriftDetector. Сохраняет результат в
// LastDriftStatus/At/Details. **Никогда не пытается auto-fix** — реконсиляция
// только по явному действию оператора через /reconcile endpoint.
//
// Audit-семантика (план PR 3.5): пишем PublicationDriftDetected=210 ТОЛЬКО на
// transition статуса И ТОЛЬКО когда новый статус ∈ {Drift, Missing, Error}.
// InSync→InSync, Drift→Drift с одинаковыми details, и Drift→InSync (последнее
// аудитится reconcile-endpoint'ом, а не drift-job'ом) — не пишутся.
internal sealed partial class DriftCheckJob : IDriftCheckJob
{
    // Audit description должен быть читаемым в UI — режем агрессивнее, чем
    // LastDriftDetails (в БД хранится до 1KB; в описании audit-row хватает ~200).
    private const int AuditDetailsSnippetLength = 200;

    private readonly AppDbContext _db;
    private readonly IIisPublishingService _iis;
    private readonly IAuditLogger _audit;
    private readonly ISettingsSnapshot _settings;
    private readonly DriftThrottleState _throttle;
    private readonly TimeProvider _clock;
    private readonly ILogger<DriftCheckJob> _logger;

    public DriftCheckJob(
        AppDbContext db,
        IIisPublishingService iis,
        IAuditLogger audit,
        ISettingsSnapshot settings,
        DriftThrottleState throttle,
        TimeProvider clock,
        ILogger<DriftCheckJob> logger)
    {
        _db = db;
        _iis = iis;
        _audit = audit;
        _settings = settings;
        _throttle = throttle;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAllAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var intervalMinutes = _settings.GetInt(SettingKey.DriftIntervalMinutes) ?? 5;

        if ((now - _throttle.LastRunAttemptUtc).TotalMinutes < intervalMinutes)
            return;

        _throttle.MarkRun(now);

        try
        {
            var ids = await _db.Publications
                .AsNoTracking()
                .Select(p => p.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                await CheckOneCoreAsync(id, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogDriftRunFailed(_logger, ex);
        }
    }

    public Task CheckOneAsync(Guid publicationId, CancellationToken ct) =>
        CheckOneCoreAsync(publicationId, ct);

    private async Task CheckOneCoreAsync(Guid publicationId, CancellationToken ct)
    {
        var publication = await _db.Publications
            .FirstOrDefaultAsync(p => p.Id == publicationId, ct)
            .ConfigureAwait(false);
        if (publication is null)
            return;

        var actual = await _iis.ReadActualStateAsync(publication, ct).ConfigureAwait(false);
        var (newStatus, newDetails) = PublicationDriftDetector.Compare(publication, actual);

        var previousStatus = publication.LastDriftStatus;
        var previousDetails = publication.LastDriftDetails;

        publication.LastDriftStatus = newStatus;
        publication.LastDriftCheckAt = _clock.GetUtcNow().UtcDateTime;
        publication.LastDriftDetails = string.IsNullOrEmpty(newDetails) ? null : newDetails;

        // tenantId нужен для audit-строки (через Infobase → Tenant). Подтягиваем
        // только если планируем писать аудит — иначе экономим один запрос.
        Guid? tenantId = null;
        var shouldAudit = IsAuditableTransition(previousStatus, previousDetails, newStatus, newDetails);
        if (shouldAudit)
        {
            tenantId = await _db.Infobases
                .Where(i => i.Id == publication.InfobaseId)
                .Select(i => (Guid?)i.TenantId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (!shouldAudit)
            return;

        var siteAndPath = $"{publication.SiteName}{publication.VirtualPath}";
        var detailsSnippet = newDetails.Length <= AuditDetailsSnippetLength
            ? newDetails
            : string.Concat(newDetails.AsSpan(0, AuditDetailsSnippetLength - 1), "…");
        var description = $"Дрейф публикации {siteAndPath}: статус {previousStatus} → {newStatus}. Детали: {detailsSnippet}";

        await _audit.LogAsync(
            AuditActionType.PublicationDriftDetected,
            initiator: "System",
            description: description,
            tenantId: tenantId,
            ct: ct).ConfigureAwait(false);
    }

    // План PR 3.5: пишем 210 ТОЛЬКО когда (status изменился) AND (новый ∈
    // {Drift, Missing, Error}). Дополнительно: Drift→Drift с теми же details
    // не считается изменением (избегаем audit-спама на стабильном дрейфе).
    private static bool IsAuditableTransition(
        PublicationDriftStatus oldStatus,
        string? oldDetails,
        PublicationDriftStatus newStatus,
        string newDetails)
    {
        if (newStatus == PublicationDriftStatus.InSync)
            return false;
        if (oldStatus != newStatus)
            return true;
        // Тот же не-InSync статус — пишем только если изменились details.
        return !string.Equals(oldDetails ?? string.Empty, newDetails, StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Drift check cycle failed")]
    private static partial void LogDriftRunFailed(ILogger logger, Exception ex);
}
