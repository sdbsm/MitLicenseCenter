using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Clusters;
using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;

namespace MitLicenseCenter.Infrastructure.Performance;

// Запись по требованию раздела «Быстродействие» (MLC-070, ADR-26, Фаза 4). Singleton: держит в
// памяти активную запись (id + старт + счётчик сэмплов) и сериализует старт/стоп/тик через один
// SemaphoreSlim. БД — через IServiceScopeFactory (паттерн HotTierPollingService: scoped AppDbContext
// и IClusterClient резолвятся внутри scope на каждую операцию). Время — через TimeProvider
// (детерминированные тесты авто-стопа). Сбор во время записи = периодические спавны rac.exe + DMV —
// осознанно включён оператором, ограничен авто-стопом (ADR-3.3/ADR-26), в отличие от live-pull,
// который идёт только пока открыта вкладка.
internal sealed partial class PerfRecordingService : IPerfRecordingService, IDisposable
{
    private const int DefaultSampleIntervalSeconds = 15;
    private const int DefaultMaxDurationMinutes = 60;
    private const int DefaultMaxSamples = 1000;

    // Сколько топ-виновников 1С/SQL сериализовать в сэмпл — ограничивает размер JSON-колонок при
    // массовой нагрузке (сотни сеансов/запросов); для атрибуции «кто грузит» хватает верхушки.
    private const int TopCulprits = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostMetricsProbe _hostProbe;
    private readonly ISqlPerformanceProbe _sqlProbe;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<PerfRecordingService> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private ActiveState? _active;
    private volatile bool _hasActive;

    public PerfRecordingService(
        IServiceScopeFactory scopeFactory,
        IHostMetricsProbe hostProbe,
        ISqlPerformanceProbe sqlProbe,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ILogger<PerfRecordingService> logger)
    {
        _scopeFactory = scopeFactory;
        _hostProbe = hostProbe;
        _sqlProbe = sqlProbe;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public bool HasActiveRecording => _hasActive;

    public async Task<PerfRecordingStartResult> StartAsync(string startedBy, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_active is not null)
            {
                return new PerfRecordingStartResult(PerfRecordingStartOutcome.AlreadyActive, _active.Id);
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            var recording = new PerfRecording
            {
                Id = Guid.NewGuid(),
                StartedAtUtc = now,
                Status = PerfRecordingStatus.Active,
                StartedBy = string.IsNullOrWhiteSpace(startedBy) ? "Unknown" : startedBy,
            };

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PerfRecordings.Add(recording);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            _active = new ActiveState(recording.Id, now);
            _hasActive = true;
            LogStarted(_logger, recording.Id, recording.StartedBy);
            return new PerfRecordingStartResult(PerfRecordingStartOutcome.Started, recording.Id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PerfRecordingStopOutcome> StopAsync(Guid recordingId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_active is null || _active.Id != recordingId)
            {
                return PerfRecordingStopOutcome.NotActive;
            }

            await FinalizeActiveLockedAsync(PerfRecordingStatus.Stopped, PerfRecordingStopReason.Manual, ct)
                .ConfigureAwait(false);
            return PerfRecordingStopOutcome.Stopped;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SampleOnceAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_active is null)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cluster = scope.ServiceProvider.GetRequiredService<IClusterClient>();

            var host = await _hostProbe.CaptureAsync(ct).ConfigureAwait(false);
            var onec = await TryCaptureOneCAsync(cluster, ct).ConfigureAwait(false);
            var sql = await TryCaptureSqlAsync(ct).ConfigureAwait(false);

            var now = _clock.GetUtcNow().UtcDateTime;
            db.PerfRecordingSamples.Add(BuildSample(_active.Id, now, host, onec, sql));
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _active.SampleCount++;

            // Авто-стоп: два независимых лимита, что наступит раньше. Проверяем после записи сэмпла,
            // так последний собранный сэмпл попадает в запись.
            var maxSamples = ClampSetting(SettingKey.PerformanceRecordingMaxSamples, DefaultMaxSamples);
            if (_active.SampleCount >= maxSamples)
            {
                await FinalizeActiveLockedAsync(PerfRecordingStatus.Stopped, PerfRecordingStopReason.SampleLimit, ct)
                    .ConfigureAwait(false);
                return;
            }

            var maxDurationMinutes = ClampSetting(SettingKey.PerformanceRecordingMaxDurationMinutes, DefaultMaxDurationMinutes);
            if (now - _active.StartedAtUtc >= TimeSpan.FromMinutes(maxDurationMinutes))
            {
                await FinalizeActiveLockedAsync(PerfRecordingStatus.Stopped, PerfRecordingStopReason.TimeLimit, ct)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Сэмпл best-effort: сбой записи/сбора логируем и оставляем запись активной — следующий
            // тик повторит. Пробы сами «never throws»; сюда попадают сбои БД/scope.
            LogSampleFailed(_logger, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecoverInterruptedAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var orphaned = await db.PerfRecordings
                .Where(r => r.Status == PerfRecordingStatus.Active)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (orphaned.Count == 0)
            {
                return;
            }

            foreach (var recording in orphaned)
            {
                recording.Status = PerfRecordingStatus.Interrupted;
                recording.StoppedAtUtc = now;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            LogRecovered(_logger, orphaned.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogRecoverFailed(_logger, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Закрывает активную запись (вызывается под _gate). status — Stopped (manual/авто) либо Interrupted.
    private async Task FinalizeActiveLockedAsync(
        PerfRecordingStatus status, PerfRecordingStopReason? reason, CancellationToken ct)
    {
        var active = _active!;
        var now = _clock.GetUtcNow().UtcDateTime;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recording = await db.PerfRecordings
            .FirstOrDefaultAsync(r => r.Id == active.Id, ct)
            .ConfigureAwait(false);

        if (recording is not null)
        {
            recording.Status = status;
            recording.StopReason = status == PerfRecordingStatus.Stopped ? reason : null;
            recording.StoppedAtUtc = now;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        _active = null;
        _hasActive = false;
        LogStopped(_logger, active.Id, status, reason, active.SampleCount);
    }

    public void Dispose() => _gate.Dispose();

    private async Task<OneCLoadSnapshot?> TryCaptureOneCAsync(IClusterClient cluster, CancellationToken ct)
    {
        var sessions = await cluster.ListSessionLoadsAsync(ct).ConfigureAwait(false);
        var processes = await cluster.ListProcessesAsync(ct).ConfigureAwait(false);

        // Пусто = rac не настроен/недоступен → виновников 1С в сэмпле нет (null), не ложный нуль.
        if (sessions.Count == 0 && processes.Count == 0)
        {
            return null;
        }

        var topSessions = sessions
            .OrderByDescending(s => Math.Max(s.CpuTimeCurrent ?? 0, s.DurationCurrent ?? 0))
            .Take(TopCulprits)
            .ToList();

        return new OneCLoadSnapshot(_clock.GetUtcNow().UtcDateTime, topSessions, processes);
    }

    private async Task<SqlPerformanceSnapshot?> TryCaptureSqlAsync(CancellationToken ct)
    {
        var snapshot = await _sqlProbe.CaptureAsync(ct).ConfigureAwait(false);

        // Degraded (нет VIEW SERVER STATE / SQL недоступен) и пусто → SQL-виновников в сэмпле нет.
        // Status сохраняем, только когда есть что показать (как live-снимок несёт честный статус).
        if (snapshot.Status != SqlProbeStatus.Ok &&
            snapshot.ActiveRequests.Count == 0 &&
            snapshot.DatabaseIo.Count == 0 &&
            snapshot.TopWaits.Count == 0)
        {
            return null;
        }

        var topRequests = snapshot.ActiveRequests
            .OrderByDescending(r => Math.Max(r.CpuTimeMs ?? 0, r.ElapsedMs ?? 0))
            .Take(TopCulprits)
            .ToList();

        return snapshot with { ActiveRequests = topRequests };
    }

    private static PerfRecordingSample BuildSample(
        Guid recordingId, DateTime sampleUtc, HostMetricsSnapshot host, OneCLoadSnapshot? onec, SqlPerformanceSnapshot? sql)
    {
        return new PerfRecordingSample
        {
            Id = Guid.NewGuid(),
            RecordingId = recordingId,
            SampleUtc = sampleUtc,
            Measuring = host.Measuring,
            CpuPercent = host.Cpu.TotalPercent,
            CpuQueueLength = host.Cpu.QueueLength,
            MemoryAvailableMBytes = host.Memory.AvailableMBytes,
            MemoryTotalMBytes = host.Memory.TotalMBytes,
            MemoryPagesPerSec = host.Memory.PagesPerSec,
            DiskAvgReadSecPerOp = host.Disk.AvgReadSecPerOp,
            DiskAvgWriteSecPerOp = host.Disk.AvgWriteSecPerOp,
            DiskQueueLength = host.Disk.QueueLength,
            ProcessesInaccessible = host.ProcessesInaccessible,
            ProcessGroupsJson = JsonSerializer.Serialize(host.ProcessGroups, PerfSampleJson.Options),
            OneCLoadJson = onec is null ? null : JsonSerializer.Serialize(onec, PerfSampleJson.Options),
            SqlLoadJson = sql is null ? null : JsonSerializer.Serialize(sql, PerfSampleJson.Options),
        };
    }

    // Клампит числовую настройку к её whitelist-диапазону (валидация на записи может отставать от
    // ужесточения диапазона; авто-стоп должен оставаться в разумных границах при любом значении).
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

    private sealed class ActiveState(Guid id, DateTime startedAtUtc)
    {
        public Guid Id { get; } = id;
        public DateTime StartedAtUtc { get; } = startedAtUtc;
        public int SampleCount { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Perf recording {RecordingId} started by {StartedBy}")]
    private static partial void LogStarted(ILogger logger, Guid recordingId, string startedBy);

    [LoggerMessage(Level = LogLevel.Information, Message = "Perf recording {RecordingId} {Status} ({Reason}), {SampleCount} sample(s)")]
    private static partial void LogStopped(ILogger logger, Guid recordingId, PerfRecordingStatus status, PerfRecordingStopReason? reason, int sampleCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recovered {Count} interrupted perf recording(s) on startup")]
    private static partial void LogRecovered(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Perf recording sample failed")]
    private static partial void LogSampleFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Perf recording interrupted-recovery failed")]
    private static partial void LogRecoverFailed(ILogger logger, Exception ex);
}
