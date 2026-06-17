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

    // Предпоказ disk-guard ДО запуска бэкапа (MLC-183): та же оценка размера базы + свободное
    // место + запас, что считает BackupAsync, но без побочных эффектов — диалог бэкапов
    // показывает оператору, хватит ли места. «Never throws»: любой сбой (нет sysadmin /
    // SQL недоступен / FILEPROPERTY вернул NULL) → degraded SqlBackupEstimate (цифры null,
    // Sufficient=false, типизированная Reason). Это предпоказ, не стоп-кран — реальный
    // disk-guard остаётся в BackupAsync.
    Task<SqlBackupEstimate> EstimateAsync(
        string server, string databaseName, string folderRoot, int safetyMarginMb, CancellationToken ct);

    // Свободное место на диске папки бэкапов в БАЙТАХ (server-side, xp_fixeddrives) — без оценки
    // размера какой-либо базы. Дешёвый host-level сигнал «мало места» для дашборда (MLC-186a):
    // сравнивается с тем же склампленным Backup.DiskSafetyMarginMb, что и предпоказ/disk-guard.
    // «Never throws»: нет sysadmin / SQL недоступен / путь не локальный диск / диск не найден → null
    // («не знаем», как degraded-цифры EstimateAsync). Отмену (OperationCanceledException) пробрасываем.
    Task<long?> GetBackupDiskFreeBytesAsync(string server, string folderRoot, CancellationToken ct);

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
