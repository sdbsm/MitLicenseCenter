using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Jobs;

// PR 4.3: батчевое удаление AuditLogs старше Settings.Audit.RetentionDays.
// Batch=5000 + commit-per-batch — избегаем lock escalation (SQL Server
// эскалирует ≈ на 5k локов; commit сбрасывает накопленные локи).
// AuditLogsPurged row пишется ТОЛЬКО если totalDeleted > 0 — не спамим
// audit на quiet days.
internal sealed partial class AuditRetentionJob : IAuditRetentionJob
{
    private const int BatchSize = 5000;
    private const int DefaultRetentionDays = 365;

    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuditRetentionJob> _logger;

    public AuditRetentionJob(
        AppDbContext db,
        IAuditLogger audit,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ILogger<AuditRetentionJob> logger)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var retentionDays = _settings.GetInt(SettingKey.AuditRetentionDays) ?? DefaultRetentionDays;
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var cutoff = nowUtc.AddDays(-retentionDays);

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
                    batchDeleted = await _db.Database.ExecuteSqlInterpolatedAsync(
                        $"DELETE TOP (5000) FROM dbo.AuditLogs WHERE [Timestamp] < {cutoff}",
                        ct).ConfigureAwait(false);
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

        if (totalDeleted == 0)
        {
            return;
        }

        var cutoffLocal = TimeZoneInfo.ConvertTimeFromUtc(cutoff, TimeZoneInfo.Local);
        var description = string.Format(
            CultureInfo.InvariantCulture,
            "Очищено {0} записей аудита старше {1:dd.MM.yyyy}.",
            totalDeleted,
            cutoffLocal);

        await _audit.LogAsync(
            AuditActionType.AuditLogsPurged,
            initiator: "System",
            description: description,
            tenantId: null,
            ct: ct).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Audit retention purge failed after deleting {Deleted} rows")]
    private static partial void LogPurgeFailed(ILogger logger, int deleted, Exception ex);
}
