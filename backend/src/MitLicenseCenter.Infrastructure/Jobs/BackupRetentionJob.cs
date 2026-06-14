using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Jobs;

// MLC-077 (ADR-27): ночная TTL-очистка бэкапов. Два прохода: (а) server-side удаление .bak
// старше cutoff (xp_delete_file через ISqlBackupService) по подпапкам всех известных баз;
// (б) reap строк DatabaseBackups старше cutoff — батчи provider-portable (паттерн
// LicenseUsageRetentionJob), каждый обёрнут в CreateExecutionStrategy().ExecuteAsync
// (MLC-074: иначе ручная транзакция падает под EnableRetryOnFailure на проде). Аудит
// BackupsPurged пишется ТОЛЬКО если удалена хотя бы одна строка — не спамим на quiet days.
internal sealed partial class BackupRetentionJob : IBackupRetentionJob
{
    private const int BatchSize = 5000;
    private const int DefaultTtlHours = 24;

    private readonly AppDbContext _db;
    private readonly ISqlBackupService _backupService;
    private readonly IAuditLogger _audit;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<BackupRetentionJob> _logger;

    public BackupRetentionJob(
        AppDbContext db,
        ISqlBackupService backupService,
        IAuditLogger audit,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ILogger<BackupRetentionJob> logger)
    {
        _db = db;
        _backupService = backupService;
        _audit = audit;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var folderRoot = _settings.GetString(SettingKey.BackupFolderPath);
        if (string.IsNullOrWhiteSpace(folderRoot))
        {
            // Папка не задана → бэкапы недоступны и чистить нечего/негде; no-op.
            LogFolderNotConfigured(_logger);
            return;
        }

        var ttlHours = ClampSetting(SettingKey.BackupTtlHours, DefaultTtlHours);
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddHours(-ttlHours);

        // (а) Файлы: подпапка каждой известной пары server+db. Идущие/свежие файлы
        // xp_delete_file не тронет (сравнивает по времени файла); вызов «never throws».
        var targets = await _db.DatabaseBackups
            .Select(b => new { b.DatabaseServer, b.DatabaseName })
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var target in targets)
        {
            var folder = Path.Combine(folderRoot, target.DatabaseName);
            var result = await _backupService
                .DeleteBackupsOlderThanAsync(target.DatabaseServer, folder, cutoff, ct)
                .ConfigureAwait(false);
            if (!result.Succeeded)
            {
                LogFileCleanupFailed(_logger, target.DatabaseName, result.ErrorMessage);
            }
        }

        // (б) Строки: телеметрия старше cutoff, НЕ трогая живую очередь (Queued/Running).
        int totalDeleted = 0;
        try
        {
            int lastBatch;
            do
            {
                // MLC-074: один батч (begin→delete→commit) = ретраибл-юнит внутри
                // CreateExecutionStrategy().ExecuteAsync; commit-per-batch сохранён.
                var batchDeleted = 0;
                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _db.Database
                        .BeginTransactionAsync(ct)
                        .ConfigureAwait(false);

                    var ids = await _db.DatabaseBackups
                        .Where(b => b.RequestedAtUtc < cutoff
                            && b.Status != BackupStatus.Queued
                            && b.Status != BackupStatus.Running)
                        .OrderBy(b => b.RequestedAtUtc)
                        .Take(BatchSize)
                        .Select(b => b.Id)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    if (ids.Count == 0)
                    {
                        await tx.CommitAsync(ct).ConfigureAwait(false);
                        batchDeleted = 0;
                        return;
                    }

                    batchDeleted = await _db.DatabaseBackups
                        .Where(b => ids.Contains(b.Id))
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

        if (totalDeleted == 0)
        {
            return;
        }

        var cutoffLocal = TimeZoneInfo.ConvertTimeFromUtc(cutoff, TimeZoneInfo.Local);
        var description = string.Format(
            CultureInfo.InvariantCulture,
            "Очищено {0} записей о бэкапах старше {1:dd.MM.yyyy HH:mm} (TTL {2} ч).",
            totalDeleted,
            cutoffLocal,
            ttlHours);

        await _audit.LogAsync(
            AuditActionType.BackupsPurged,
            initiator: "System",
            description: description,
            tenantId: null,
            ct: ct).ConfigureAwait(false);
    }

    // Кламп к whitelist-диапазону настройки (паттерн PerfRecordingService/BackupOrchestrator).
    private int ClampSetting(string key, int fallback)
    {
        var value = _settings.GetInt(key) ?? fallback;
        var def = SettingDefinitions.All[key];
        if (def.Min is { } min && value < min)
        {
            value = min;
        }

        if (def.Max is { } max && value > max)
        {
            value = max;
        }

        return value;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Backup retention skipped: Backup.FolderPath is not configured")]
    private static partial void LogFolderNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Backup retention: server-side file cleanup failed for database {Database}: {Error}")]
    private static partial void LogFileCleanupFailed(ILogger logger, string database, string? error);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Backup retention purge failed after deleting {Deleted} rows")]
    private static partial void LogPurgeFailed(ILogger logger, int deleted, Exception ex);
}
