namespace MitLicenseCenter.Application.Backups;

// Оркестратор очереди бэкапов (MLC-077, ADR-27). Реализация — BackupOrchestrator
// (Infrastructure, singleton): очередь = таблица DatabaseBackups (Queued-строки — FIFO,
// Running — выполняющиеся), in-memory набор выполняющихся пар (server, db) даёт
// замок-на-базу. Драйвер — BackupPumpService (BackgroundService): на старте
// RecoverInterruptedAsync, дальше цикл «wake-сигнал ИЛИ таймаут» → PumpOnceAsync.
public interface IBackupOrchestrator
{
    // Поставить бэкап базы в очередь. Если у пары (server, databaseName) уже есть
    // Queued/Running строка — AlreadyActive с id СУЩЕСТВУЮЩЕЙ (эндпоинт отдаёт 409
    // BACKUP_ACTIVE); иначе вставляет Queued и будит насос.
    Task<BackupRequestResult> RequestAsync(
        Guid infobaseId, string server, string databaseName, string requestedBy, CancellationToken ct);

    // Один тик насоса (тест-шов, образец IPerfRecordingService.SampleOnceAsync): перечитать
    // Backup.MaxParallel (изменение действует со следующего тика, без рестарта) и, пока
    // running < max, стартовать самые старые Queued, чьи (server, db) не выполняются —
    // FIFO + замок-на-базу + потолок одним проходом. Сами бэкапы идут параллельно вне тика.
    Task PumpOnceAsync(CancellationToken ct);

    // Старт приложения: осиротевшие Running (рестарт панели оборвал выполнение) →
    // Failed/Interrupted; Queued не трогаются — насос их переподхватит.
    Task RecoverInterruptedAsync(CancellationToken ct);

    // Точка ожидания насоса: возвращается по wake-сигналу (новый запрос / завершение
    // бэкапа) либо по таймауту — что наступит раньше. Таймаут не ошибка, а плановый тик.
    Task WaitForWakeAsync(TimeSpan timeout, CancellationToken ct);
}
