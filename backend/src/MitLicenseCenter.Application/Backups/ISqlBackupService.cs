namespace MitLicenseCenter.Application.Backups;

// Порт on-demand бэкапа базы SQL (MLC-076, ADR-20/27). Реализация — SqlBackupAdapter
// (Infrastructure, чистый ADO.NET); в тестах — FakeSqlBackupService. Web и оркестратор
// (MLC-077) никогда не зовут BACKUP / xp_* напрямую — только этот интерфейс.
public interface ISqlBackupService
{
    // Весь безопасный цикл одного бэкапа (ADR-27): sysadmin-проверка → база существует →
    // оценка размера → проверка места (оценка + safetyMarginMb) → подпапка →
    // BACKUP … WITH COPY_ONLY + RESTORE VERIFYONLY → только после verify удалить прошлый
    // .bak базы (keep-latest-1, новый-перед-удалением — никогда наоборот). Никогда не
    // бросает ради инфраструктурного сбоя: любой провал → типизированный
    // SqlBackupResult (Reason + текст), как degraded-статусы ISqlPerformanceProbe.
    Task<SqlBackupResult> BackupAsync(
        string server, string databaseName, string folderRoot, int safetyMarginMb, CancellationToken ct);

    // Server-side удаление .bak старше cutoffUtc в папке folderPath (xp_delete_file) —
    // для TTL-ретенции (MLC-077) и Admin-удаления бэкапа. Тоже «never throws».
    Task<SqlDeleteResult> DeleteBackupsOlderThanAsync(
        string server, string folderPath, DateTime cutoffUtc, CancellationToken ct);
}
