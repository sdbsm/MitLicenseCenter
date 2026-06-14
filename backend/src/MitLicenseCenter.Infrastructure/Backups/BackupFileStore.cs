using Microsoft.Extensions.Logging;

namespace MitLicenseCenter.Infrastructure.Backups;

// Чистые файловые операции цикла бэкапа (MLC-152, ADR-28: single-host). Вынесены из адаптера
// в общий хелпер, чтобы покрыть юнит-тестами на реальном каталоге и переиспользовать в обоих
// сценариях удаления (keep-latest-1 после verify в BackupAsync и TTL-ретенция через
// DeleteBackupsOlderThanAsync). Заменяют расширенную процедуру xp_delete_file, которая требовала
// серверной роли sysadmin.
internal static partial class BackupFileStore
{
    // Удаляет из каталога файлы «*.bak» строго старше cutoffUtc (по LastWriteTimeUtc) — то же
    // отсечение по времени, что давал xp_delete_file. Чужие расширения не трогает. Отсутствие
    // каталога — no-op (как раньше: нечего чистить). Возвращает число удалённых файлов.
    //
    // Сравнение строгое (`<`): файл с временем РОВНО cutoff переживает — keep-latest-1 в
    // BackupAsync передаёт cutoff = момент старта BACKUP, а только что записанный .bak имеет
    // LastWriteTimeUtc позже старта (запись завершилась после), поэтому он остаётся, а все
    // прошлые .bak базы (старше старта) удаляются.
    //
    // Ошибку удаления ОДНОГО файла (открыт другим процессом, гонка с параллельным бэкапом)
    // глотаем и логируем, продолжая по остальным — best-effort retention не должен валить
    // всю операцию; фатальные IOException/UnauthorizedAccessException на уровне перечисления
    // каталога пробрасываются вызывающему (он маппит их в SqlDeleteResult/PermissionDenied).
    public static int DeleteBackupsOlderThan(string folder, DateTime cutoffUtc, ILogger logger)
    {
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        var cutoff = DateTime.SpecifyKind(cutoffUtc, DateTimeKind.Utc);
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(folder, "*.bak", SearchOption.TopDirectoryOnly))
        {
            DateTime lastWriteUtc;
            try
            {
                lastWriteUtc = File.GetLastWriteTimeUtc(file);
            }
            catch (IOException ex)
            {
                LogSkippedFile(logger, file, ex);
                continue;
            }
            catch (UnauthorizedAccessException ex)
            {
                LogSkippedFile(logger, file, ex);
                continue;
            }

            if (lastWriteUtc >= cutoff)
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (IOException ex)
            {
                LogSkippedFile(logger, file, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                LogSkippedFile(logger, file, ex);
            }
        }

        return deleted;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Бэкап БД: не удалось удалить устаревший файл {File}, пропущен")]
    private static partial void LogSkippedFile(ILogger logger, string file, Exception ex);
}
