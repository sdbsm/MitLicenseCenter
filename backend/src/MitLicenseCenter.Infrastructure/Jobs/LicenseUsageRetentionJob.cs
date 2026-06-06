using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Jobs;

// MLC-048 (ADR-25): батчевое удаление LicenseUsageSnapshots старше
// Settings.LicenseUsage.RetentionDays. Batch=5000 + commit-per-batch — избегаем lock
// escalation (SQL Server эскалирует ≈ на 5k локов; commit сбрасывает накопленные локи).
// В отличие от AuditRetentionJob удаление сделано provider-portable (SELECT id'шников →
// ExecuteDelete WHERE Id IN ...), а не raw `DELETE TOP` — транслируется и на SQL Server,
// и на SQLite, поэтому ретеншен покрыт юнит-тестом. Без аудит-записи: это housekeeping
// телеметрии (хватит server-лога; запись в аудит жгла бы новый замороженный enum-номер).
internal sealed partial class LicenseUsageRetentionJob : ILicenseUsageRetentionJob
{
    private const int BatchSize = 5000;
    private const int DefaultRetentionDays = 365;

    private readonly AppDbContext _db;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<LicenseUsageRetentionJob> _logger;

    public LicenseUsageRetentionJob(
        AppDbContext db,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ILogger<LicenseUsageRetentionJob> logger)
    {
        _db = db;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var retentionDays = _settings.GetInt(SettingKey.LicenseUsageRetentionDays) ?? DefaultRetentionDays;
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-retentionDays);

        int totalDeleted = 0;
        try
        {
            int lastBatch;
            do
            {
                await using var tx = await _db.Database
                    .BeginTransactionAsync(ct)
                    .ConfigureAwait(false);

                var ids = await _db.LicenseUsageSnapshots
                    .Where(x => x.BucketStartUtc < cutoff)
                    .OrderBy(x => x.BucketStartUtc)
                    .Take(BatchSize)
                    .Select(x => x.Id)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                if (ids.Count == 0)
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    break;
                }

                lastBatch = await _db.LicenseUsageSnapshots
                    .Where(x => ids.Contains(x.Id))
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);

                await tx.CommitAsync(ct).ConfigureAwait(false);
                totalDeleted += lastBatch;
                ct.ThrowIfCancellationRequested();
            } while (lastBatch == BatchSize);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogPurgeFailed(_logger, totalDeleted, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "License usage retention purge failed after deleting {Deleted} rows")]
    private static partial void LogPurgeFailed(ILogger logger, int deleted, Exception ex);
}
