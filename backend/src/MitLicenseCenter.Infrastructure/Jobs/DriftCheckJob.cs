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
            // PERF-07 (MLC-043): один проекционный запрос нужных полей всех публикаций
            // вместо «грузим Id → FirstOrDefault на каждую» (N+1 round-trips на проход).
            // AsNoTracking + только поля, которые читает проверка дрейфа (без тяжёлого
            // VrdCustomXml и без tracking-снимков на весь объём). Запись результата
            // дрейфа — отдельным targeted-UPDATE по Id в ProcessOneAsync, поведение 1:1.
            var snapshots = await LoadSnapshotsAsync(p => true, ct).ConfigureAwait(false);

            foreach (var snapshot in snapshots)
            {
                ct.ThrowIfCancellationRequested();
                await ProcessOneAsync(snapshot, ct).ConfigureAwait(false);
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

    public async Task CheckOneAsync(Guid publicationId, CancellationToken ct)
    {
        // On-demand путь (reconcile / check-drift endpoint): тот же проекционный
        // снимок, но для одной публикации. ProcessOneAsync обрабатывает запись 1:1.
        var snapshots = await LoadSnapshotsAsync(p => p.Id == publicationId, ct).ConfigureAwait(false);
        var snapshot = snapshots.Count > 0 ? snapshots[0] : null;
        if (snapshot is null)
            return;

        await ProcessOneAsync(snapshot, ct).ConfigureAwait(false);
    }

    // Проекция строго того, что нужно проверке дрейфа: поля для ReadActualState/Compare
    // (desired), InfobaseId (для tenant audit-строки) и предыдущие LastDriftStatus/Details
    // (для определения audit-перехода). AsNoTracking — снимок не трекается; VrdCustomXml
    // и прочие тяжёлые/ненужные колонки не тянутся.
    private async Task<List<DriftSnapshot>> LoadSnapshotsAsync(
        System.Linq.Expressions.Expression<Func<Publication, bool>> filter,
        CancellationToken ct) =>
        await _db.Publications
            .AsNoTracking()
            .Where(filter)
            .Select(p => new DriftSnapshot(
                p.Id,
                p.InfobaseId,
                p.SiteName,
                p.VirtualPath,
                p.PlatformVersion,
                p.EnableOData,
                p.EnableHttpServices,
                p.PhysicalPathOverride,
                p.LastDriftStatus,
                p.LastDriftDetails))
            .ToListAsync(ct)
            .ConfigureAwait(false);

    private async Task ProcessOneAsync(DriftSnapshot snapshot, CancellationToken ct)
    {
        // Транзиентный desired для ReadActualState/Compare — те же входные поля,
        // что читал tracked-entity раньше (Site/VirtualPath/PhysicalPathOverride +
        // PlatformVersion/OData/Http). 1:1 по сравнению.
        var desired = new Publication
        {
            Id = snapshot.Id,
            InfobaseId = snapshot.InfobaseId,
            SiteName = snapshot.SiteName,
            VirtualPath = snapshot.VirtualPath,
            PlatformVersion = snapshot.PlatformVersion,
            EnableOData = snapshot.EnableOData,
            EnableHttpServices = snapshot.EnableHttpServices,
            PhysicalPathOverride = snapshot.PhysicalPathOverride,
        };

        var actual = await _iis.ReadActualStateAsync(desired, ct).ConfigureAwait(false);
        var (newStatus, newDetails) = PublicationDriftDetector.Compare(desired, actual);

        var previousStatus = snapshot.PreviousStatus;
        var previousDetails = snapshot.PreviousDetails;
        var newDetailsOrNull = string.IsNullOrEmpty(newDetails) ? null : newDetails;

        // Targeted-апдейт только трёх drift-колонок по Id — поведение записи 1:1
        // с прежним «mutate tracked + SaveChanges». Если сущность уже трекается в
        // текущем scope (напр. reconcile-endpoint в том же DbContext) — мутируем её,
        // иначе attach'им транзиентный desired и помечаем Modified только 3 поля.
        var tracked = _db.Publications.Local.FirstOrDefault(p => p.Id == snapshot.Id);
        var target = tracked ?? desired;
        if (tracked is null)
            _db.Attach(desired);

        target.LastDriftStatus = newStatus;
        target.LastDriftCheckAt = _clock.GetUtcNow().UtcDateTime;
        target.LastDriftDetails = newDetailsOrNull;

        // tenantId нужен для audit-строки (через Infobase → Tenant). Подтягиваем
        // только если планируем писать аудит — иначе экономим один запрос.
        Guid? tenantId = null;
        var shouldAudit = IsAuditableTransition(previousStatus, previousDetails, newStatus, newDetails);
        if (shouldAudit)
        {
            tenantId = await _db.Infobases
                .Where(i => i.Id == snapshot.InfobaseId)
                .Select(i => (Guid?)i.TenantId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (!shouldAudit)
            return;

        var siteAndPath = $"{snapshot.SiteName}{snapshot.VirtualPath}";
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

    // Лёгкий проекционный снимок публикации для одного прохода проверки дрейфа.
    // PreviousStatus/PreviousDetails — значения LastDrift* ДО проверки (вход для
    // IsAuditableTransition). Остальные поля — desired-состояние для Compare/IIS.
    private sealed record DriftSnapshot(
        Guid Id,
        Guid InfobaseId,
        string SiteName,
        string VirtualPath,
        string PlatformVersion,
        bool EnableOData,
        bool EnableHttpServices,
        string? PhysicalPathOverride,
        PublicationDriftStatus PreviousStatus,
        string? PreviousDetails);

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
