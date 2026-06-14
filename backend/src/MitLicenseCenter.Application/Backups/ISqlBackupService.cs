namespace MitLicenseCenter.Application.Backups;

// Порт on-demand бэкапа базы SQL (MLC-076, ADR-20/27). Реализация — SqlBackupAdapter
// (Infrastructure, чистый ADO.NET); в тестах — FakeSqlBackupService. Web и оркестратор
// (MLC-077) никогда не зовут BACKUP / xp_* напрямую — только этот интерфейс.
public interface ISqlBackupService
{
    // Весь безопасный цикл одного бэкапа (ADR-27/28): база существует → проба права
    // BACKUP DATABASE (db_owner, sysadmin не нужен — MLC-152) → оценка размера → проверка
    // места (оценка + safetyMarginMb, .NET DriveInfo) → подпапка (Directory.CreateDirectory) →
    // BACKUP … WITH COPY_ONLY + RESTORE VERIFYONLY → только после verify удалить прошлый
    // .bak базы (keep-latest-1, новый-перед-удалением — никогда наоборот). Никогда не
    // бросает ради инфраструктурного сбоя: любой провал → типизированный
    // SqlBackupResult (Reason + текст), как degraded-статусы ISqlPerformanceProbe.
    Task<SqlBackupResult> BackupAsync(
        string server, string databaseName, string folderRoot, int safetyMarginMb, CancellationToken ct);

    // Удаление .bak старше cutoffUtc в папке folderPath (File.Delete по LastWriteTimeUtc) —
    // для TTL-ретенции (MLC-077) и Admin-удаления бэкапа. На single-host (ADR-28) — чисто
    // файловая операция этого узла (MLC-152). Тоже «never throws».
    Task<SqlDeleteResult> DeleteBackupsOlderThanAsync(
        string server, string folderPath, DateTime cutoffUtc, CancellationToken ct);
}
