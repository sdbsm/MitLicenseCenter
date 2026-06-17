using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Jobs;

// MLC-185c (близнец LicenseUsageRetentionJob): батчевое удаление DatabaseSizeSnapshots
// старше Settings.DatabaseSize.RetentionDays. Batch=5000 + commit-per-batch — избегаем lock
// escalation (SQL Server эскалирует ≈ на 5k локов; commit сбрасывает накопленные локи).
// Удаление provider-portable (SELECT id'шников → ExecuteDelete WHERE Id IN ...), а не raw
// `DELETE TOP` — транслируется и на SQL Server, и на SQLite, поэтому ретеншен покрыт
// юнит-тестом. Без аудит-записи: это housekeeping телеметрии (хватит server-лога; запись в
// аудит жгла бы новый замороженный enum-номер).
internal sealed partial class DatabaseSizeRetentionJob : IDatabaseSizeRetentionJob
{
    private const int BatchSize = 5000;
    private const int DefaultRetentionDays = 365;

    private readonly AppDbContext _db;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<DatabaseSizeRetentionJob> _logger;

    public DatabaseSizeRetentionJob(
        AppDbContext db,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ILogger<DatabaseSizeRetentionJob> logger)
    {
        _db = db;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var retentionDays = _settings.GetInt(SettingKey.DatabaseSizeRetentionDays) ?? DefaultRetentionDays;
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-retentionDays);

        int totalDeleted = 0;
        try
        {
            int lastBatch;
            do
            {
                // MLC-074: при включённом EnableRetryOnFailure (SqlServerRetryingExecutionStrategy)
                // ручная BeginTransactionAsync вне стратегии бросает "...does not support
                // user-initiated transactions". Оборачиваем ОДИН батч (begin→delete→commit) в
                // CreateExecutionStrategy().ExecuteAsync — стратегия повторяет батч как
                // ретраибл-юнит. Внешний do-while (накопление totalDeleted) остаётся снаружи:
                // каждый батч — отдельная ретраибл-транзакция, commit-per-batch сохранён.
                var batchDeleted = 0;
                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _db.Database
                        .BeginTransactionAsync(ct)
                        .ConfigureAwait(false);

                    var ids = await _db.DatabaseSizeSnapshots
                        .Where(x => x.SnapshotAtUtc < cutoff)
                        .OrderBy(x => x.SnapshotAtUtc)
                        .Take(BatchSize)
                        .Select(x => x.Id)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    if (ids.Count == 0)
                    {
                        await tx.CommitAsync(ct).ConfigureAwait(false);
                        batchDeleted = 0;
                        return;
                    }

                    batchDeleted = await _db.DatabaseSizeSnapshots
                        .Where(x => ids.Contains(x.Id))
                        .ExecuteDeleteAsync(ct)
                        .ConfigureAwait(false);

                    await tx.CommitAsync(ct).ConfigureAwait(false);
                }).ConfigureAwait(false);

                lastBatch = batchDeleted;
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
        Message = "Database size retention purge failed after deleting {Deleted} rows")]
    private static partial void LogPurgeFailed(ILogger logger, int deleted, Exception ex);
}
