using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Auditing;
using MitLicenseCenter.Application.Backups;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Audit;
using MitLicenseCenter.Domain.Settings;
using MitLicenseCenter.Infrastructure.Persistence;
using MitLicenseCenter.Infrastructure.Reporting;

namespace MitLicenseCenter.Infrastructure.Backups;

// Оркестратор очереди бэкапов (MLC-077, ADR-27). Singleton (образец PerfRecordingService):
// учётные операции (постановка в очередь, старт, финиш, recovery) сериализуются одним
// SemaphoreSlim; БД и scoped IAuditLogger — через IServiceScopeFactory; время — TimeProvider.
// Осознанное отличие от PerfRecordingService: сам долгий BACKUP идёт ВНЕ gate — бэкапы
// разных баз параллельны по замыслу (потолок Backup.MaxParallel), gate держит только
// быструю бухгалтерию. Очередь durable: Queued/Running — строки DatabaseBackups, поэтому
// рестарт панели не теряет запросы (Queued переподхватятся, Running → Interrupted).
internal sealed partial class BackupOrchestrator : IBackupOrchestrator, IDisposable
{
    private const int DefaultMaxParallel = 2;
    private const int DefaultDiskSafetyMarginMb = 2048;

    // Потолок времени одного COPY_ONLY-бэкапа: дольше него Running считается зависшим и
    // подлежит reap'у (MLC-123). 6 ч — разумный верхний предел даже для крупной базы на
    // медленном диске; реальный бэкап завершается на порядки быстрее, поэтому ложных
    // срабатываний нет, а настоящий вис (повисший xp_cmdshell/сетевой шар/драйвер ленты)
    // не блокирует базу навсегда. Внутренний knob — константа в коде, не Setting (как
    // фиксированный CRON retention-джоб и JobRetentionStateFilter): отдельная настройка
    // потянула бы enum+каталог+миграцию+FE+i18n (отложено в MLC-026) ради того, что
    // оператор не крутит.
    private static readonly TimeSpan StuckRunningTimeout = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISqlBackupService _backupService;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<BackupOrchestrator> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    // Wake-сигнал насоса, ёмкость 1 — повторные сигналы коалесцируются (насос за один тик
    // и так разбирает всё, что готово стартовать).
    private readonly SemaphoreSlim _wake = new(0, 1);
    // Выполняющиеся пары server+db (замок-на-базу). Имена в SQL Server регистронезависимы.
    private readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);

    public BackupOrchestrator(
        IServiceScopeFactory scopeFactory,
        ISqlBackupService backupService,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ILogger<BackupOrchestrator> logger)
    {
        _scopeFactory = scopeFactory;
        _backupService = backupService;
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BackupRequestResult> RequestAsync(
        Guid infobaseId, string server, string databaseName, string requestedBy, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var activeId = await db.DatabaseBackups
                .Where(b => b.DatabaseServer == server
                    && b.DatabaseName == databaseName
                    && (b.Status == BackupStatus.Queued || b.Status == BackupStatus.Running))
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (activeId is { } existing)
            {
                return new BackupRequestResult(BackupRequestOutcome.AlreadyActive, existing);
            }

            var backup = new DatabaseBackup
            {
                Id = Guid.NewGuid(),
                InfobaseId = infobaseId,
                DatabaseServer = server,
                DatabaseName = databaseName,
                Status = BackupStatus.Queued,
                RequestedBy = string.IsNullOrWhiteSpace(requestedBy) ? "Unknown" : requestedBy,
                RequestedAtUtc = _clock.GetUtcNow().UtcDateTime,
            };
            db.DatabaseBackups.Add(backup);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            LogQueued(_logger, backup.Id, databaseName, backup.RequestedBy);
            Wake();
            return new BackupRequestResult(BackupRequestOutcome.Queued, backup.Id);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PumpOnceAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Перечитывается каждый тик: смена Backup.MaxParallel действует со следующего
            // тика без рестарта (снижение не убивает идущие — лишь не стартует новые).
            var maxParallel = ClampSetting(SettingKey.BackupMaxParallel, DefaultMaxParallel);
            if (_running.Count >= maxParallel)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var queued = await db.DatabaseBackups
                .Where(b => b.Status == BackupStatus.Queued)
                .OrderBy(b => b.RequestedAtUtc)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var backup in queued)
            {
                if (_running.Count >= maxParallel)
                {
                    break;
                }

                // Замок-на-базу: вторая Queued той же пары ждёт завершения первой.
                if (!_running.Add(RunningKey(backup.DatabaseServer, backup.DatabaseName)))
                {
                    continue;
                }

                try
                {
                    backup.Status = BackupStatus.Running;
                    backup.StartedAtUtc = _clock.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                }
                catch
                {
                    _running.Remove(RunningKey(backup.DatabaseServer, backup.DatabaseName));
                    throw;
                }

                LogStarted(_logger, backup.Id, backup.DatabaseName);

                // Долгий BACKUP — вне gate, в своей задаче и свежем scope. Намеренно без
                // внешнего ct: оборвать BACKUP на сервере мы всё равно не можем; рестарт
                // панели закрывает строку через RecoverInterruptedAsync.
                var (id, server, database) = (backup.Id, backup.DatabaseServer, backup.DatabaseName);
                _ = Task.Run(() => ExecuteBackupAsync(id, server, database), CancellationToken.None);
            }
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
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var orphaned = await db.DatabaseBackups
                .Where(b => b.Status == BackupStatus.Running)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (orphaned.Count == 0)
            {
                return;
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            foreach (var backup in orphaned)
            {
                backup.Status = BackupStatus.Failed;
                backup.FailureReason = BackupFailureReason.Interrupted;
                backup.CompletedAtUtc = now;
                // Таймстамп-имя + verify-before-delete не дают принять обрывок за валидный
                // бэкап; TTL-джоба его подчистит.
                backup.ErrorMessage = "Бэкап оборван перезапуском панели — файл на диске может быть неполным.";
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

    public async Task ReapStuckRunningAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = _clock.GetUtcNow().UtcDateTime;
            var cutoff = now - StuckRunningTimeout;

            // «Возраст» Running считаем от StartedAtUtc (момент перехода в Running); если по
            // какой-то причине он null — fallback на RequestedAtUtc, чтобы строка без отметки
            // старта всё равно подлежала reap'у, а не висела вечно.
            var stuck = await db.DatabaseBackups
                .Where(b => b.Status == BackupStatus.Running
                    && (b.StartedAtUtc ?? b.RequestedAtUtc) < cutoff)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (stuck.Count == 0)
            {
                return;
            }

            var timeoutHours = (int)StuckRunningTimeout.TotalHours;
            foreach (var backup in stuck)
            {
                backup.Status = BackupStatus.Failed;
                backup.FailureReason = BackupFailureReason.TimedOut;
                backup.CompletedAtUtc = now;
                backup.ErrorMessage =
                    $"Бэкап превысил лимит времени выполнения ({timeoutHours} ч) и помечен как " +
                    "зависший — файл на диске может быть неполным.";

                // КРИТИЧНО: снять in-memory замок-на-базу. Без этого база осталась бы
                // заблокированной до рестарта, даже после закрытия строки в БД.
                _running.Remove(RunningKey(backup.DatabaseServer, backup.DatabaseName));
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            LogReaped(_logger, stuck.Count);

            // Аудит — только когда что-то reap'нули (тихие тики не спамим). Action-first
            // standalone-запись (R3): reaper не парен с мутацией-запросом в одном контексте.
            // Переиспользуем замороженный BackupFailed (нового enum-номера не заводим).
            var databases = string.Join(", ", stuck
                .Select(b => b.DatabaseName)
                .Distinct(StringComparer.OrdinalIgnoreCase));
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
            await audit.LogAsync(
                AuditActionType.BackupFailed,
                initiator: "System",
                description: $"TTL-reaper закрыл {stuck.Count} зависш(их) бэкап(ов) " +
                    $"(превышен лимит {timeoutHours} ч): {databases}.",
                tenantId: null,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogReapFailed(_logger, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task WaitForWakeAsync(TimeSpan timeout, CancellationToken ct)
        // Результат не важен: false (таймаут) — это плановый тик насоса.
        => await _wake.WaitAsync(timeout, ct).ConfigureAwait(false);

    public void Dispose()
    {
        _gate.Dispose();
        _wake.Dispose();
    }

    // Весь жизненный цикл одной операции после старта: настройки → BackupAsync (адаптер
    // сам делает keep-latest после VERIFYONLY — оркестратор файлы НЕ удаляет) → финиш.
    private async Task ExecuteBackupAsync(Guid backupId, string server, string databaseName)
    {
        SqlBackupResult result;
        try
        {
            // Папку читаем к моменту старта (эндпоинт уже проверял при постановке, но
            // настройку могли очистить, пока запрос ждал в очереди) — честный Failed.
            var folderRoot = _settings.GetString(SettingKey.BackupFolderPath);
            if (string.IsNullOrWhiteSpace(folderRoot))
            {
                result = new SqlBackupResult(
                    Succeeded: false,
                    Reason: BackupFailureReason.BackupFailed,
                    FilePath: null,
                    FileSizeBytes: null,
                    ErrorMessage: "Корневая папка для бэкапов не задана (настройка Backup.FolderPath) — бэкап не выполнен.");
            }
            else
            {
                var marginMb = ClampSetting(SettingKey.BackupDiskSafetyMarginMb, DefaultDiskSafetyMarginMb);
                result = await _backupService
                    .BackupAsync(server, databaseName, folderRoot, marginMb, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Порт «never throws», но фоновая задача не имеет права умереть молча —
            // иначе строка зависнет в Running до рестарта.
            LogBackupCrashed(_logger, databaseName, ex);
            result = new SqlBackupResult(
                Succeeded: false,
                Reason: BackupFailureReason.BackupFailed,
                FilePath: null,
                FileSizeBytes: null,
                ErrorMessage: "Внутренняя ошибка выполнения бэкапа — технические подробности записаны в журнал сервера.");
        }

        await CompleteAsync(backupId, server, databaseName, result).ConfigureAwait(false);
    }

    // Финиш под gate: строка → Succeeded/Failed + аудит 511/512 (initiator = RequestedBy
    // строки; tenantId не пишем — у телеметрии нет FK, связку с клиентом несёт
    // эндпоинт-аудит BackupRequested). Затем снять замок и разбудить насос.
    private async Task CompleteAsync(Guid backupId, string server, string databaseName, SqlBackupResult result)
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Строку могли удалить (Admin DELETE для Queued) — тогда просто снимаем замок.
            var backup = await db.DatabaseBackups
                .FirstOrDefaultAsync(b => b.Id == backupId)
                .ConfigureAwait(false);

            // Гонка с reaper'ом (MLC-123): зависший Task.Run мог вернуться УЖЕ ПОСЛЕ того,
            // как TTL-reaper закрыл эту строку (Failed/TimedOut) и снял замок. Не
            // перетираем терминальный статус и не пишем второй аудит — просто проваливаемся
            // в finally к снятию замка (Remove идемпотентен, ключа уже нет).
            if (backup is { Status: BackupStatus.Running })
            {
                backup.CompletedAtUtc = _clock.GetUtcNow().UtcDateTime;
                if (result.Succeeded)
                {
                    backup.Status = BackupStatus.Succeeded;
                    backup.FilePath = result.FilePath;
                    backup.FileSizeBytes = result.FileSizeBytes;
                }
                else
                {
                    backup.Status = BackupStatus.Failed;
                    backup.FailureReason = result.Reason;
                    backup.ErrorMessage = result.ErrorMessage;
                }

                await db.SaveChangesAsync().ConfigureAwait(false);

                var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
                if (result.Succeeded)
                {
                    await audit.LogAsync(
                        AuditActionType.BackupSucceeded,
                        initiator: backup.RequestedBy,
                        description: $"Бэкап базы «{databaseName}» выполнен успешно. Файл: {result.FilePath}.")
                        .ConfigureAwait(false);
                }
                else
                {
                    await audit.LogAsync(
                        AuditActionType.BackupFailed,
                        initiator: backup.RequestedBy,
                        description: $"Бэкап базы «{databaseName}» завершился ошибкой: {result.ErrorMessage ?? result.Reason.ToString()}")
                        .ConfigureAwait(false);
                }
            }

            LogCompleted(_logger, backupId, databaseName, result.Succeeded, result.Reason);
        }
        catch (Exception ex)
        {
            // Best-effort: статус/аудит не записались (сбой БД) — замок всё равно снимаем,
            // иначе база навсегда выпадет из бэкапов до рестарта.
            LogCompleteFailed(_logger, backupId, ex);
        }
        finally
        {
            _running.Remove(RunningKey(server, databaseName));
            _gate.Release();
            Wake();
        }
    }

    private void Wake()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // Сигнал уже взведён — коалесцируем.
        }
    }

    private static string RunningKey(string server, string databaseName) => server + "\n" + databaseName;

    // Клампит числовую настройку к её whitelist-диапазону (паттерн PerfRecordingService):
    // валидация на записи могла отстать от ужесточения диапазона, а потолок параллельных
    // обязан оставаться в разумных границах при любом значении.
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup {BackupId} of database {Database} queued by {RequestedBy}")]
    private static partial void LogQueued(ILogger logger, Guid backupId, string database, string requestedBy);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup {BackupId} of database {Database} started")]
    private static partial void LogStarted(ILogger logger, Guid backupId, string database);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup {BackupId} of database {Database} completed: Succeeded={Succeeded} ({Reason})")]
    private static partial void LogCompleted(ILogger logger, Guid backupId, string database, bool succeeded, BackupFailureReason reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Backup of database {Database} crashed outside the adapter")]
    private static partial void LogBackupCrashed(ILogger logger, string database, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to finalize backup {BackupId}")]
    private static partial void LogCompleteFailed(ILogger logger, Guid backupId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recovered {Count} interrupted backup(s) on startup")]
    private static partial void LogRecovered(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Backup interrupted-recovery failed")]
    private static partial void LogRecoverFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reaped {Count} stuck Running backup(s) past the execution-time limit")]
    private static partial void LogReaped(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stuck-Running backup reaper failed")]
    private static partial void LogReapFailed(ILogger logger, Exception ex);
}
