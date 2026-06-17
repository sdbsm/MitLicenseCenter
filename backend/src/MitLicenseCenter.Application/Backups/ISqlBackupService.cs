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

    // Живой флаг доступности файлов бэкапов на диске SQL-хоста (MLC-178): для каждого пути из
    // paths — есть ли там файл (server-side xp_fileexist). Список в карточке инфобазы берётся
    // из БД и может расходиться с фактом (ручное удаление, keep-latest вытеснил файл, TTL-чистка
    // удалила файл раньше строки) — этот метод сверяет с диском. «Never throws»: любой сбой
    // (SqlException/InvalidOperationException/нет sysadmin/SQL недоступен) → ПУСТОЙ словарь
    // = «не знаем». Пустой paths → пустой словарь без обращения к SQL. Ключи результата —
    // ровно запрошенные пути (присутствующие); отсутствие пути в словаре трактуется как «не знаем».
    Task<IReadOnlyDictionary<string, bool>> FilesExistAsync(
        string server, IReadOnlyCollection<string> paths, CancellationToken ct);
}
