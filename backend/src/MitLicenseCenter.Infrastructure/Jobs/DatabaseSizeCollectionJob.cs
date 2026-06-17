using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Reporting;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;

namespace MitLicenseCenter.Infrastructure.Jobs;

// MLC-185c: суточный сбор размеров баз. Охват — только базы инфобаз (решение владельца):
// один замер IDatabaseSizeProbe (по всем базам инстанса) фильтруется по DatabaseName баз
// инфобаз; для каждого совпадения пишется DatabaseSizeSnapshot с единым SnapshotAtUtc
// (момент старта прогона). Сопоставление имён баз — case-insensitive в памяти (SQL-имена
// могут отличаться регистром от записи инфобазы); это in-memory свёртка, не EF-запрос, —
// StringComparison тут легален (на SQL не транслируется). Базы инфобаз без показания
// (база отсутствует/недоступна) пропускаются — нулевой снимок не пишем. Базы инстанса вне
// инфобаз игнорируются. Без аудит-записи — это фоновая телеметрия (как cold-цикл
// ReconciliationJob).
internal sealed partial class DatabaseSizeCollectionJob : IDatabaseSizeCollectionJob
{
    private readonly AppDbContext _db;
    private readonly IDatabaseSizeProbe _probe;
    private readonly TimeProvider _clock;
    private readonly ILogger<DatabaseSizeCollectionJob> _logger;

    public DatabaseSizeCollectionJob(
        AppDbContext db,
        IDatabaseSizeProbe probe,
        TimeProvider clock,
        ILogger<DatabaseSizeCollectionJob> logger)
    {
        _db = db;
        _probe = probe;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var snapshotAtUtc = _clock.GetUtcNow().UtcDateTime;

        try
        {
            // Базы инфобаз: каноничное имя (как записано в инфобазе) + привязка к тенанту.
            // Свёртка по имени case-insensitive — последняя инфобаза с данным именем
            // «выигрывает» (в норме имена баз уникальны; коллизия регистра не ожидается).
            var infobases = await _db.Infobases
                .AsNoTracking()
                .Select(i => new { i.DatabaseName, i.TenantId })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var canonicalByName = new Dictionary<string, (string DatabaseName, Guid TenantId)>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var ib in infobases)
                canonicalByName[ib.DatabaseName] = (ib.DatabaseName, ib.TenantId);

            // Один замер — показания по всем базам инстанса.
            var readings = await _probe.ReadSizesAsync(ct).ConfigureAwait(false);

            var snapshots = new List<DatabaseSizeSnapshot>();
            foreach (var r in readings)
            {
                if (!canonicalByName.TryGetValue(r.DatabaseName, out var ib))
                    continue; // база инстанса вне инфобаз — игнорируем

                snapshots.Add(new DatabaseSizeSnapshot
                {
                    Id = Guid.NewGuid(),
                    TenantId = ib.TenantId,
                    // Пишем каноничное имя из инфобазы (а не из показания SQL): имя инфобазы —
                    // ключ сопоставления отчётов; единый регистр стабилизирует индекс/группировку.
                    DatabaseName = ib.DatabaseName,
                    SnapshotAtUtc = snapshotAtUtc,
                    DataBytes = r.DataBytes,
                    LogBytes = r.LogBytes,
                });
            }

            if (snapshots.Count == 0)
                return; // нет совпадений — ничего не пишем (без пустого SaveChanges)

            _db.DatabaseSizeSnapshots.AddRange(snapshots);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);

            LogCollected(_logger, snapshots.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCollectFailed(_logger, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Database size collection: {Count} snapshots written")]
    private static partial void LogCollected(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database size collection failed")]
    private static partial void LogCollectFailed(ILogger logger, Exception ex);
}
