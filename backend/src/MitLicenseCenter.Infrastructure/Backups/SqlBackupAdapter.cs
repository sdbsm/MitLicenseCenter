using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Backups;

namespace MitLicenseCenter.Infrastructure.Backups;

// Реальный адаптер on-demand бэкапа базы SQL (MLC-076, ADR-27/28). Исполняет весь безопасный
// цикл одной операции. По ADR-28 (single-host) панель и SQL Server живут на ОДНОМ узле, поэтому
// файловые шаги (свободное место, создание каталога, удаление старых .bak) выполняются обычными
// .NET-вызовами от имени аккаунта панели — без расширенных процедур xp_fixeddrives /
// xp_create_subdir / xp_delete_file, которые требовали серверной роли sysadmin (MLC-152).
// В SQL остаются только операции, покрываемые db_owner: BACKUP DATABASE и RESTORE VERIFYONLY.
//   (1) база существует в sys.databases;
//   (2) право BACKUP DATABASE по базе (HAS_PERMS_BY_NAME); нет права → честный PermissionDenied
//       (а не загадочная ошибка BACKUP на полпути);
//   (3) оценка размера = занятые ROWS-страницы (FILEPROPERTY 'SpaceUsed'); нет оценки → НЕ стартуем;
//   (4) свободное место — .NET DriveInfo по корню каталога бэкапа; требуем оценку + запас
//       (disk-guard ADR-27);
//   (5) Directory.CreateDirectory подпапки базы (идемпотентно);
//   (6) BACKUP DATABASE … WITH COPY_ONLY, COMPRESSION, CHECKSUM, FORMAT, INIT (COPY_ONLY —
//       несущая опция: не сбрасывает differential base, не ломает внешнюю дифф-цепочку);
//   (7) RESTORE VERIFYONLY WITH CHECKSUM — ДО удаления старого;
//   (8) только после verify — File.Delete прошлых .bak базы (keep-latest-1,
//       новый-перед-удалением, никогда наоборот);
//   (9) размер готового файла — File.Length по факту (best-effort: нет файла → null, не провал).
//
// Инъекции исключены конструктивно: имя базы — идентификатор, проверяется по sys.databases и
// попадает в динамический SQL только через QUOTENAME; путь — всегда строковый параметр или
// локальная файловая операция.
//
// Подключение наследует параметры из ConnectionStrings:Default (как SqlPerformanceProbe/
// SqlDatabaseDiscovery): DataSource=server вызова, InitialCatalog=master. «Never throws» —
// инфраструктурный сбой деградирует в типизированный SqlBackupResult/SqlDeleteResult
// (catch SqlException/InvalidOperationException/IOException/UnauthorizedAccessException), отмена
// (OperationCanceledException) пробрасывается. НЕ Windows-only: чистый ADO.NET + System.IO, без
// [SupportedOSPlatform]/CA1416. Stateless → singleton.
internal sealed partial class SqlBackupAdapter : ISqlBackupService
{
    private const int ConnectTimeoutSeconds = 15;
    private const int MetadataCommandTimeoutSeconds = 30;
    // BACKUP/VERIFY больших баз идут десятки минут и часы — таймаут пробы (5с) здесь
    // категорически не годится.
    private const int BackupCommandTimeoutSeconds = 4 * 60 * 60;

    private readonly string? _baseConnectionString;
    private readonly TimeProvider _clock;
    private readonly ILogger<SqlBackupAdapter> _logger;

    public SqlBackupAdapter(
        IConfiguration configuration, TimeProvider clock, ILogger<SqlBackupAdapter> logger)
    {
        _baseConnectionString = configuration.GetConnectionString("Default");
        _clock = clock;
        _logger = logger;
    }

    public async Task<SqlBackupResult> BackupAsync(
        string server, string databaseName, string folderRoot, int safetyMarginMb, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return Fail(BackupFailureReason.BackupFailed,
                "Строка подключения ConnectionStrings:Default не настроена.");
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString(server));
            await connection.OpenAsync(ct).ConfigureAwait(false);

            // (1)
            if (!await DatabaseExistsAsync(connection, databaseName, ct).ConfigureAwait(false))
            {
                return Fail(BackupFailureReason.BackupFailed,
                    $"База «{databaseName}» не найдена на сервере «{server}».");
            }

            // (2) Честный degraded-сигнал вместо загадочной ошибки BACKUP на полпути
            // (паттерн HasViewServerStateAsync пробы MLC-068). sysadmin БОЛЬШЕ НЕ НУЖЕН
            // (MLC-152, ADR-28): достаточно права BACKUP DATABASE (входит в db_owner).
            if (!await HasBackupPermissionAsync(connection, databaseName, ct).ConfigureAwait(false))
            {
                return Fail(BackupFailureReason.PermissionDenied,
                    $"Учётной записи панели не выдано право BACKUP DATABASE по базе «{databaseName}» " +
                    "(требуется членство в роли db_owner этой базы). " +
                    "См. OPERATIONS.md «Бэкап — required permissions».");
            }

            // (3) Оценка по занятым данным — сознательный over-estimate (COMPRESSION ужмёт
            // реальный .bak); NULL → не стартуем (ADR-27: нет оценки — нет бэкапа).
            var estimateKb = await ReadUsedDataKbAsync(connection, databaseName, ct).ConfigureAwait(false);
            if (estimateKb is null or <= 0)
            {
                return Fail(BackupFailureReason.EstimateUnavailable,
                    $"Не удалось оценить размер базы «{databaseName}» (FILEPROPERTY вернул NULL) — " +
                    "без оценки бэкап не стартует (защита диска).");
            }

            // (4) Свободное место — .NET DriveInfo по корню каталога бэкапа. На single-host
            // (ADR-28) каталог — локальный диск этого же узла; UNC/относительный путь не имеет
            // корня диска → отклоняем явно, а не пропуском проверки места.
            var freeMb = TryGetFreeSpaceMb(folderRoot);
            if (freeMb is null)
            {
                return Fail(BackupFailureReason.BackupFailed,
                    $"Не удалось определить свободное место для каталога бэкапов «{folderRoot}». " +
                    "Путь должен указывать на локальный диск этого узла в виде «D:\\Backups» " +
                    "(UNC и относительные пути не поддерживаются).");
            }

            var estimateMb = estimateKb.Value / 1024;
            var requiredMb = estimateMb + safetyMarginMb;
            if (freeMb.Value < requiredMb)
            {
                return Fail(BackupFailureReason.InsufficientSpace,
                    $"Недостаточно места для каталога «{folderRoot}»: свободно {freeMb.Value} МБ, требуется " +
                    $"не менее {requiredMb} МБ (данные базы ≈{estimateMb} МБ + запас {safetyMarginMb} МБ).");
            }

            // (5)
            var subfolder = Path.Combine(folderRoot, databaseName);
            Directory.CreateDirectory(subfolder);

            // База времени файловых операций — ЛОКАЛЬНОЕ время этого узла. Панель и SQL
            // co-located на одном хосте (single-node, ADR-28), удаление старых .bak делает
            // сама панель (File.Delete), поэтому сравнение идёт с LastWriteTimeUtc файлов.
            // Имя файла несёт момент старта BACKUP — служит cutoff'ом keep-latest-1:
            // прошлый .bak старше старта — удаляется, новый (создан после) — переживает.
            var startedAtLocal = _clock.GetLocalNow().DateTime;
            var startedAtUtc = _clock.GetUtcNow().UtcDateTime;
            var fileName = string.Create(CultureInfo.InvariantCulture,
                $"{databaseName}_{startedAtLocal:yyyyMMdd_HHmmss}.bak");
            var path = Path.Combine(subfolder, fileName);

            // (6) + (7)
            await RunBackupAsync(connection, databaseName, path, ct).ConfigureAwait(false);
            await VerifyBackupAsync(connection, path, ct).ConfigureAwait(false);

            // (8) Только после успешного verify — иначе провал нового бэкапа оставил бы
            // базу вовсе без бэкапа. Удаляем .bak строго старше момента старта → только что
            // созданный файл переживает (keep-latest-1).
            BackupFileStore.DeleteBackupsOlderThan(subfolder, startedAtUtc, _logger);

            // (9)
            var sizeBytes = TryReadFileSize(path);
            return new SqlBackupResult(true, BackupFailureReason.None, path, sizeBytes, null);
        }
        catch (SqlException ex)
        {
            LogBackupFailed(_logger, server, databaseName, ex);
            return Fail(BackupFailureReason.BackupFailed, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Битая строка подключения / соединение умерло посреди операции.
            LogBackupFailed(_logger, server, databaseName, ex);
            return Fail(BackupFailureReason.BackupFailed, ex.Message);
        }
        catch (IOException ex)
        {
            // CreateDirectory/проверка места упали на файловом уровне.
            LogBackupFailed(_logger, server, databaseName, ex);
            return Fail(BackupFailureReason.BackupFailed, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Аккаунту панели не хватает NTFS-прав на каталог бэкапов.
            LogBackupFailed(_logger, server, databaseName, ex);
            return Fail(BackupFailureReason.PermissionDenied,
                $"Аккаунту панели не хватает прав на каталог бэкапов: {ex.Message}");
        }
    }

    public Task<SqlDeleteResult> DeleteBackupsOlderThanAsync(
        string server, string folderPath, DateTime cutoffUtc, CancellationToken ct)
    {
        // server-параметр сохранён в контракте (ISqlBackupService) для симметрии и совместимости
        // с BackupAsync; на single-host (ADR-28) удаление — чисто файловая операция этого узла,
        // SQL-подключение не требуется.
        ct.ThrowIfCancellationRequested();

        try
        {
            var deleted = BackupFileStore.DeleteBackupsOlderThan(folderPath, cutoffUtc, _logger);
            LogDeleted(_logger, server, folderPath, deleted);
            return Task.FromResult(new SqlDeleteResult(true, null));
        }
        catch (IOException ex)
        {
            LogDeleteFailed(_logger, server, folderPath, ex);
            return Task.FromResult(new SqlDeleteResult(false, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDeleteFailed(_logger, server, folderPath, ex);
            return Task.FromResult(new SqlDeleteResult(false, ex.Message));
        }
    }

    private static SqlBackupResult Fail(BackupFailureReason reason, string message) =>
        new(false, reason, null, null, message);

    private string BuildConnectionString(string server) =>
        new SqlConnectionStringBuilder(_baseConnectionString)
        {
            DataSource = server,
            InitialCatalog = "master",
            ConnectTimeout = ConnectTimeoutSeconds,
        }.ConnectionString;

    // Свободное место в МБ по корню каталога бэкапа через .NET DriveInfo (MLC-152: замена
    // xp_fixeddrives, не требует sysadmin). На single-host (ADR-28) каталог — локальный диск
    // этого узла. UNC/относительный путь не имеет корня вида «D:\» → null (как раньше отклоняли).
    internal static long? TryGetFreeSpaceMb(string folderRoot)
    {
        var root = TryGetLocalDriveRoot(folderRoot);
        if (root is null)
        {
            return null;
        }

        try
        {
            var drive = new DriveInfo(root);
            if (!drive.IsReady)
            {
                return null;
            }

            return drive.AvailableFreeSpace / (1024 * 1024);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    // Корень локального диска вида «D:\» из пути «D:\Backups…». UNC и относительные пути
    // корня не имеют → null. Буква нормализуется в верхний регистр.
    internal static string? TryGetLocalDriveRoot(string folderRoot)
    {
        if (folderRoot.Length < 3 || folderRoot[1] != ':' || folderRoot[2] is not ('\\' or '/'))
        {
            return null;
        }

        if (!char.IsAsciiLetter(folderRoot[0]))
        {
            return null;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{char.ToUpperInvariant(folderRoot[0])}:\\");
    }

    private static long? TryReadFileSize(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ── SQL-шаги цикла (db_owner-покрытие) ──────────────────────────────────────────

    private static async Task<bool> DatabaseExistsAsync(
        SqlConnection connection, string databaseName, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sys.databases WHERE name = @db;";
        command.CommandTimeout = MetadataCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@db", databaseName);
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is not null;
    }

    // Лёгкая проба права BACKUP DATABASE по конкретной базе (MLC-152: заменяет sysadmin-precheck).
    // HAS_PERMS_BY_NAME(db, 'DATABASE', 'BACKUP DATABASE') → 1, если у логина есть это право
    // (член db_owner его имеет). Возвращает честный PermissionDenied ДО запуска BACKUP.
    private static async Task<bool> HasBackupPermissionAsync(
        SqlConnection connection, string databaseName, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT HAS_PERMS_BY_NAME(QUOTENAME(@db), N'DATABASE', N'BACKUP DATABASE');";
        command.CommandTimeout = MetadataCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@db", databaseName);
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is int i && i == 1;
    }

    // Занятые ROWS-страницы базы в КБ (страница = 8 КБ). Контекст базы — через динамический
    // USE QUOTENAME(@db); FILEPROPERTY работает только в контексте своей базы.
    private static async Task<long?> ReadUsedDataKbAsync(
        SqlConnection connection, string databaseName, CancellationToken ct)
    {
        const string sql = @"
DECLARE @sql nvarchar(max) = N'USE ' + QUOTENAME(@db) + N';
SELECT @est = SUM(CAST(FILEPROPERTY(name, ''SpaceUsed'') AS bigint)) * 8
FROM sys.database_files
WHERE type_desc = N''ROWS'';';
EXEC sys.sp_executesql @sql, N'@est bigint OUTPUT', @est = @estKb OUTPUT;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = MetadataCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@db", databaseName);
        var output = command.Parameters.Add("@estKb", System.Data.SqlDbType.BigInt);
        output.Direction = System.Data.ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return output.Value is long kb ? kb : null;
    }

    private static async Task RunBackupAsync(
        SqlConnection connection, string databaseName, string path, CancellationToken ct)
    {
        const string sql = @"
DECLARE @sql nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(@db) +
    N' TO DISK = @path WITH COPY_ONLY, COMPRESSION, CHECKSUM, FORMAT, INIT, STATS = 10;';
EXEC sys.sp_executesql @sql, N'@path nvarchar(512)', @path = @path;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = BackupCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@db", databaseName);
        command.Parameters.AddWithValue("@path", path);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task VerifyBackupAsync(
        SqlConnection connection, string path, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;";
        command.CommandTimeout = BackupCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@path", path);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Бэкап БД: операция провалилась (сервер {Server}, база {Database})")]
    private static partial void LogBackupFailed(ILogger logger, string server, string database, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Бэкап БД: удалено {Deleted} устаревших файлов (сервер {Server}, папка {Folder})")]
    private static partial void LogDeleted(ILogger logger, string server, string folder, int deleted);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Бэкап БД: удаление устаревших файлов провалилось (сервер {Server}, папка {Folder})")]
    private static partial void LogDeleteFailed(ILogger logger, string server, string folder, Exception ex);
}
