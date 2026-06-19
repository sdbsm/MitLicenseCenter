namespace MitLicenseCenter.Application.Maintenance;

// Порт пробы обслуживания SQL (MLC-216, ADR-54). Live-read свежести резервных копий баз из
// msdb.dbo.backupset — БЕЗ собственных таблиц/миграций/джоб (вкладка «Обслуживание» раздела
// «Сервер» — только чтение). Реализация — SqlMaintenanceProbe (Infrastructure, чистый ADO.NET);
// в тестах — ручной фейк (NSubstitute не проксирует internal-адаптеры Infrastructure).
//
// Структура расширяема: MLC-217 (планы обслуживания sysmaintplan_* + SQL Agent) дорастит ЭТУ
// же пробу новым методом/полями снимка, не ломая контракт свежести бэкапов.
public interface IMaintenanceProbe
{
    // Снимок свежести бэкапов по всем пользовательским базам инстанса (live-read backupset).
    // «Never throws»: нет прав на msdb.dbo.backupset → Status=PermissionDenied; нет SQL / нет
    // строки подключения → Status=Unavailable; в обоих случаях Databases пуст. Отмена
    // (OperationCanceledException) пробрасывается.
    Task<BackupFreshnessSnapshot> GetBackupFreshnessAsync(CancellationToken ct);

    // Снимок планов обслуживания (MLC-217, live-read msdb.dbo.sysmaintplan_* + история заданий
    // SQL Agent). «Never throws»: нет прав на историю планов → Status=PermissionDenied; SQL Agent
    // отсутствует/остановлен (Express) или нет прав на историю заданий → Status=AgentUnavailable;
    // нет SQL / нет строки подключения → Status=Unavailable; в degraded-ветках Plans пуст.
    // Отмена (OperationCanceledException) пробрасывается.
    Task<MaintenancePlansSnapshot> GetMaintenancePlansAsync(CancellationToken ct);
}
