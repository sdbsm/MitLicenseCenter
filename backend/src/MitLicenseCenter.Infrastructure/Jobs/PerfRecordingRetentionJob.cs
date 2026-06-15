using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Jobs;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Infrastructure.Persistence;

namespace MitLicenseCenter.Infrastructure.Jobs;

// MLC-169: батчевое удаление записей «Быстродействия» старше RetentionDays. Двухфазно и
// построчно-батчево, как LicenseUsageRetentionJob, но с поправкой на родитель→дети: одна
// PerfRecording несёт до Performance.RecordingMaxSamples сэмплов, поэтому каскад не
// используем (он снёс бы все сэмплы старых записей одним DELETE → lock escalation; к тому же
// семантика каскада на SQLite/SQL Server расходится). Фаза 1 чистит сэмплы старых записей
// батчами по 5000 строк, фаза 2 — освободившиеся записи. Provider-portable (SELECT id'шников
// → ExecuteDelete WHERE Id IN ...) — транслируется и на SQL Server, и на SQLite, покрыто
// юнит-тестом. Срок — константа, не Setting (прецедент JobRetentionStateFilter). Активную
// запись (Status=Active) не трогаем — её сэмплит фоновый таймер. Без аудит-записи: housekeeping.
internal sealed partial class PerfRecordingRetentionJob : IPerfRecordingRetentionJob
{
    private const int BatchSize = 5000;

    // Срок хранения записей «Быстродействия». Зашит константой (не настройка) — это отладочные
    // артефакты «по требованию», оператор их сроком жизни не управляет. 90 дней: успеть
    // разобрать инцидент, но не копить годами тяжёлые JSON-сэмплы.
    private const int RetentionDays = 90;

    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<PerfRecordingRetentionJob> _logger;

    public PerfRecordingRetentionJob(
        AppDbContext db,
        TimeProvider clock,
        ILogger<PerfRecordingRetentionJob> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-RetentionDays);

        var samplesDeleted = 0;
        var recordingsDeleted = 0;
        try
        {
            // Фаза 1: сэмплы старых терминальных записей (Status != Active). Построчные батчи —
            // одна запись может нести десятки тысяч сэмплов, не сносим их одним DELETE.
            samplesDeleted = await PurgeBatchedAsync(
                c => _db.PerfRecordingSamples
                    .Where(s => _db.PerfRecordings.Any(r =>
                        r.Id == s.RecordingId &&
                        r.Status != PerfRecordingStatus.Active &&
                        r.StartedAtUtc < cutoff))
                    .Take(BatchSize)
                    .Select(s => s.Id)
                    .ToListAsync(c),
                (ids, c) => _db.PerfRecordingSamples
                    .Where(s => ids.Contains(s.Id))
                    .ExecuteDeleteAsync(c),
                ct).ConfigureAwait(false);

            // Фаза 2: освободившиеся от сэмплов старые записи. Активную НЕ трогаем.
            recordingsDeleted = await PurgeBatchedAsync(
                c => _db.PerfRecordings
                    .Where(r => r.Status != PerfRecordingStatus.Active && r.StartedAtUtc < cutoff)
                    .OrderBy(r => r.StartedAtUtc)
                    .Take(BatchSize)
                    .Select(r => r.Id)
                    .ToListAsync(c),
                (ids, c) => _db.PerfRecordings
                    .Where(r => ids.Contains(r.Id))
                    .ExecuteDeleteAsync(c),
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogPurgeFailed(_logger, samplesDeleted, recordingsDeleted, ex);
            throw;
        }
    }

    // Построчно-батчевое удаление с commit-per-batch (избегаем lock escalation ≈ на 5k локов).
    // MLC-074: при включённом EnableRetryOnFailure ручная BeginTransactionAsync вне стратегии
    // бросает "...does not support user-initiated transactions" — каждый батч (begin→select→
    // delete→commit) оборачиваем в CreateExecutionStrategy().ExecuteAsync (стратегия повторяет
    // батч как ретраибл-юнит). Внешний do-while — снаружи, commit-per-batch сохранён.
    private async Task<int> PurgeBatchedAsync(
        Func<CancellationToken, Task<List<Guid>>> selectIds,
        Func<List<Guid>, CancellationToken, Task<int>> deleteByIds,
        CancellationToken ct)
    {
        var total = 0;
        int lastBatch;
        do
        {
            var batchDeleted = 0;
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database
                    .BeginTransactionAsync(ct)
                    .ConfigureAwait(false);

                var ids = await selectIds(ct).ConfigureAwait(false);

                if (ids.Count == 0)
                {
                    await tx.CommitAsync(ct).ConfigureAwait(false);
                    batchDeleted = 0;
                    return;
                }

                batchDeleted = await deleteByIds(ids, ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            lastBatch = batchDeleted;
            total += lastBatch;
            ct.ThrowIfCancellationRequested();
        } while (lastBatch == BatchSize);

        return total;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Perf recording retention purge failed after deleting {Samples} samples / {Recordings} recordings")]
    private static partial void LogPurgeFailed(ILogger logger, int samples, int recordings, Exception ex);
}
