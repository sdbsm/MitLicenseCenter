using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Backups;

namespace MitLicenseCenter.Infrastructure.Backups;

// Реальный адаптер on-demand бэкапа базы SQL (MLC-076, ADR-27). Исполняет весь безопасный
// цикл одной операции server-side (панель не трогает файловую систему SQL-хоста):
//   (0) sysadmin-проверка IS_SRVROLEMEMBER — без роли xp_* недоступны → честный PermissionDenied;
//   (1) база существует в sys.databases;
//   (2) оценка размера = занятые ROWS-страницы (FILEPROPERTY 'SpaceUsed'); нет оценки → НЕ стартуем;
//   (3) folderRoot — только локальный диск SQL-хоста; свободное место через xp_fixeddrives,
//       требуем оценку + запас (disk-guard ADR-27);
//   (4) xp_create_subdir подпапки базы (идемпотентно);
//   (5) BACKUP DATABASE … WITH COPY_ONLY, COMPRESSION, CHECKSUM, FORMAT, INIT (COPY_ONLY —
//       несущая опция: не сбрасывает differential base, не ломает внешнюю дифф-цепочку);
//   (6) RESTORE VERIFYONLY WITH CHECKSUM — ДО удаления старого;
//   (7) только после verify — xp_delete_file прошлых .bak базы (keep-latest-1,
//       новый-перед-удалением, никогда наоборот);
//   (8) размер готового файла из msdb.dbo.backupset (best-effort: не нашли → null, не провал).
//
// Инъекции исключены конструктивно: имя базы — идентификатор, проверяется по sys.databases и
// попадает в динамический SQL только через QUOTENAME; путь — всегда строковый параметр.
//
// Подключение наследует параметры из ConnectionStrings:Default (как SqlPerformanceProbe/
// SqlDatabaseDiscovery): DataSource=server вызова, InitialCatalog=master. «Never throws» —
// инфраструктурный сбой деградирует в типизированный SqlBackupResult/SqlDeleteResult
// (catch SqlException/InvalidOperationException), отмена (OperationCanceledException)
// пробрасывается. НЕ Windows-only: чистый ADO.NET, без [SupportedOSPlatform]/CA1416.
// Stateless → singleton.
internal sealed partial class SqlBackupAdapter : ISqlBackupService
{
    private const int ConnectTimeoutSeconds = 15;
    private const int MetadataCommandTimeoutSeconds = 30;
    // BACKUP/VERIFY больших баз идут десятки минут и часы — таймаут пробы (5с) здесь
    // категорически не годится.
    private const int BackupCommandTimeoutSeconds = 4 * 60 * 60;
    private const int DeleteCommandTimeoutSeconds = 10 * 60;

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

            // (0) Честный degraded-сигнал вместо загадочных ошибок xp_* на полпути
            // (паттерн HasViewServerStateAsync пробы MLC-068).
            if (!await IsSysadminAsync(connection, ct).ConfigureAwait(false))
            {
                return Fail(BackupFailureReason.PermissionDenied,
                    "Учётной записи панели не выдана серверная роль sysadmin — BACKUP и файловые " +
                    "операции (xp_create_subdir/xp_fixeddrives/xp_delete_file) недоступны. " +
                    "См. OPERATIONS.md «Бэкап — required permissions».");
            }

            // (1)
            if (!await DatabaseExistsAsync(connection, databaseName, ct).ConfigureAwait(false))
            {
                return Fail(BackupFailureReason.BackupFailed,
                    $"База «{databaseName}» не найдена на сервере «{server}».");
            }

            // (2) + (3) disk-guard вынесен в ComputeEstimateAsync (MLC-183) — та же оценка,
            // что показывает предпоказ EstimateAsync; здесь только переводим её degraded-причину
            // в прежний человекочитаемый текст (поведение байт-эквивалентно). NULL-оценка → не
            // стартуем (ADR-27: нет оценки — нет бэкапа); нехватка места — стоп-кран.
            var estimate = await ComputeEstimateAsync(
                connection, databaseName, folderRoot, safetyMarginMb, ct).ConfigureAwait(false);
            if (!estimate.Sufficient)
            {
                return Fail(estimate.Reason, DescribeGuardFailure(estimate, databaseName, folderRoot, safetyMarginMb));
            }

            // (4)
            var subfolder = Path.Combine(folderRoot, databaseName);
            await CreateSubdirAsync(connection, subfolder, ct).ConfigureAwait(false);

            // База времени файловых операций — ЛОКАЛЬНОЕ время SQL-хоста: xp_delete_file
            // сравнивает cutoff с файловыми timestamp'ами в местном времени, а панель и SQL
            // co-located на одном хосте (single-node, ADR-26/27) → локальное время панели
            // и SQL-хоста совпадает. Момент старта BACKUP служит cutoff'ом keep-latest-1:
            // прошлый .bak старше старта — удаляется, новый (создан после) — переживает.
            var startedAtLocal = _clock.GetLocalNow().DateTime;
            var fileName = string.Create(CultureInfo.InvariantCulture,
                $"{databaseName}_{startedAtLocal:yyyyMMdd_HHmmss}.bak");
            var path = Path.Combine(subfolder, fileName);

            // (5) + (6)
            await RunBackupAsync(connection, databaseName, path, ct).ConfigureAwait(false);
            await VerifyBackupAsync(connection, path, ct).ConfigureAwait(false);

            // (7) Только после успешного verify — иначе провал нового бэкапа оставил бы
            // базу вовсе без бэкапа.
            await DeleteFilesOlderThanAsync(connection, subfolder, startedAtLocal, ct).ConfigureAwait(false);

            // (8)
            var sizeBytes = await TryReadBackupSizeAsync(connection, path, ct).ConfigureAwait(false);
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
    }

    // Предпоказ disk-guard (MLC-183): открыть коннект → sysadmin-гейт → база существует →
    // ComputeEstimateAsync (та же оценка, что в BackupAsync) → SqlBackupEstimate. «Never throws»:
    // любой сбой деградирует в SqlBackupEstimate(null, null, margin*1MiB, false, причина).
    public async Task<SqlBackupEstimate> EstimateAsync(
        string server, string databaseName, string folderRoot, int safetyMarginMb, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return DegradedEstimate(safetyMarginMb, BackupFailureReason.BackupFailed);
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString(server));
            await connection.OpenAsync(ct).ConfigureAwait(false);

            if (!await IsSysadminAsync(connection, ct).ConfigureAwait(false))
            {
                return DegradedEstimate(safetyMarginMb, BackupFailureReason.PermissionDenied);
            }

            if (!await DatabaseExistsAsync(connection, databaseName, ct).ConfigureAwait(false))
            {
                return DegradedEstimate(safetyMarginMb, BackupFailureReason.BackupFailed);
            }

            return await ComputeEstimateAsync(connection, databaseName, folderRoot, safetyMarginMb, ct)
                .ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            LogBackupFailed(_logger, server, databaseName, ex);
            return DegradedEstimate(safetyMarginMb, BackupFailureReason.BackupFailed);
        }
        catch (InvalidOperationException ex)
        {
            LogBackupFailed(_logger, server, databaseName, ex);
            return DegradedEstimate(safetyMarginMb, BackupFailureReason.BackupFailed);
        }
    }

    // Свободное место диска папки бэкапов в байтах (MLC-186a): тот же путь, что disk-guard
    // (sysadmin-гейт → буква диска → xp_fixeddrives), но без оценки размера базы — host-level
    // сигнал «мало места» для дашборда. «Never throws»: любая degraded-ветка (нет sysadmin /
    // путь не локальный диск / диск не найден / SQL недоступен) → null. Отмену пробрасываем.
    public async Task<long?> GetBackupDiskFreeBytesAsync(
        string server, string folderRoot, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_baseConnectionString) || !TryGetDriveLetter(folderRoot, out var drive))
        {
            return null;
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString(server));
            await connection.OpenAsync(ct).ConfigureAwait(false);

            if (!await IsSysadminAsync(connection, ct).ConfigureAwait(false))
            {
                return null;
            }

            var freeMb = await ReadFreeMbAsync(connection, drive, ct).ConfigureAwait(false);
            return freeMb is { } mb ? mb * BytesPerMb : null;
        }
        catch (SqlException ex)
        {
            LogBackupFailed(_logger, server, folderRoot, ex);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            LogBackupFailed(_logger, server, folderRoot, ex);
            return null;
        }
    }

    public async Task<SqlDeleteResult> DeleteBackupsOlderThanAsync(
        string server, string folderPath, DateTime cutoffUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return new SqlDeleteResult(false, "Строка подключения ConnectionStrings:Default не настроена.");
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString(server));
            await connection.OpenAsync(ct).ConfigureAwait(false);

            if (!await IsSysadminAsync(connection, ct).ConfigureAwait(false))
            {
                return new SqlDeleteResult(false,
                    "Учётной записи панели не выдана серверная роль sysadmin — xp_delete_file недоступна. " +
                    "См. OPERATIONS.md «Бэкап — required permissions».");
            }

            // UTC контракта порта → локальное время SQL-хоста (см. комментарий в BackupAsync).
            var cutoffLocal = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(cutoffUtc, DateTimeKind.Utc), TimeZoneInfo.Local);
            await DeleteFilesOlderThanAsync(connection, folderPath, cutoffLocal, ct).ConfigureAwait(false);
            return new SqlDeleteResult(true, null);
        }
        catch (SqlException ex)
        {
            LogDeleteFailed(_logger, server, folderPath, ex);
            return new SqlDeleteResult(false, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            LogDeleteFailed(_logger, server, folderPath, ex);
            return new SqlDeleteResult(false, ex.Message);
        }
    }

    // Живой флаг доступности файлов на диске SQL-хоста (MLC-178). Server-side через
    // xp_fileexist — на каждый путь EXEC master.dbo.xp_fileexist @path возвращает строку с
    // колонкой «File Exists» (0/1). Один открытый коннект на весь список (round-trip на список,
    // не на файл); тот же sysadmin-гейт, что у остальных xp_*-методов. «Never throws»: любой
    // сбой → пустой словарь («не знаем»). Пустой paths → пустой словарь без обращения к SQL.
    public async Task<IReadOnlyDictionary<string, bool>> FilesExistAsync(
        string server, IReadOnlyCollection<string> paths, CancellationToken ct)
    {
        if (paths.Count == 0 || string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return EmptyResult;
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString(server));
            await connection.OpenAsync(ct).ConfigureAwait(false);

            if (!await IsSysadminAsync(connection, ct).ConfigureAwait(false))
            {
                return EmptyResult;
            }

            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                result[path] = await FileExistsAsync(connection, path, ct).ConfigureAwait(false);
            }

            return result;
        }
        catch (SqlException ex)
        {
            LogFilesExistFailed(_logger, server, ex);
            return EmptyResult;
        }
        catch (InvalidOperationException ex)
        {
            LogFilesExistFailed(_logger, server, ex);
            return EmptyResult;
        }
    }

    private static readonly IReadOnlyDictionary<string, bool> EmptyResult =
        new Dictionary<string, bool>(StringComparer.Ordinal);

    private static SqlBackupResult Fail(BackupFailureReason reason, string message) =>
        new(false, reason, null, null, message);

    private const long BytesPerMb = 1024L * 1024L;

    // Ровно те же шаги disk-guard, что были inline в BackupAsync (MLC-183): оценка размера базы
    // (ReadUsedDataKbAsync) → буква диска (TryGetDriveLetter) → свободное место (ReadFreeMbAsync) →
    // сравнение «оценка + запас ≤ свободно». Единственная реализация — её зовут и BackupAsync
    // (стоп-кран), и EstimateAsync (предпоказ). Все числа конвертируются в БАЙТЫ. Любая degraded-
    // ветка (NULL-оценка / путь не локальный диск / диск не найден) → Sufficient=false с
    // типизированной причиной; человекочитаемый текст для BackupAsync собирает DescribeGuardFailure.
    private static async Task<SqlBackupEstimate> ComputeEstimateAsync(
        SqlConnection connection, string databaseName, string folderRoot, int safetyMarginMb, CancellationToken ct)
    {
        var marginBytes = (long)safetyMarginMb * BytesPerMb;

        // (2) Оценка по занятым данным — сознательный over-estimate (COMPRESSION ужмёт
        // реальный .bak); NULL → не стартуем (ADR-27: нет оценки — нет бэкапа).
        var estimateKb = await ReadUsedDataKbAsync(connection, databaseName, ct).ConfigureAwait(false);
        if (estimateKb is null or <= 0)
        {
            return new SqlBackupEstimate(null, null, marginBytes, false, BackupFailureReason.EstimateUnavailable);
        }

        var estimateMb = estimateKb.Value / 1024;
        var estimateBytes = estimateMb * BytesPerMb;

        // (3) xp_fixeddrives видит только локальные диски — UNC/относительный путь
        // отклоняем явно, а не пропуском проверки места.
        if (!TryGetDriveLetter(folderRoot, out var drive))
        {
            return new SqlBackupEstimate(estimateBytes, null, marginBytes, false, BackupFailureReason.BackupFailed);
        }

        var freeMb = await ReadFreeMbAsync(connection, drive, ct).ConfigureAwait(false);
        if (freeMb is null)
        {
            return new SqlBackupEstimate(estimateBytes, null, marginBytes, false, BackupFailureReason.BackupFailed);
        }

        var freeBytes = freeMb.Value * BytesPerMb;
        var sufficient = freeMb.Value >= estimateMb + safetyMarginMb;
        return new SqlBackupEstimate(
            estimateBytes, freeBytes, marginBytes, sufficient,
            sufficient ? BackupFailureReason.None : BackupFailureReason.InsufficientSpace);
    }

    // Воспроизводит прежний человекочитаемый текст BackupAsync для каждой degraded-ветки
    // disk-guard (поведение байт-эквивалентно). Числа берём из estimate (байты → МБ обратимы,
    // т.к. в ComputeEstimateAsync хранятся как целые МБ × 1 МиБ).
    private static string DescribeGuardFailure(
        SqlBackupEstimate estimate, string databaseName, string folderRoot, int safetyMarginMb)
    {
        if (estimate.Reason == BackupFailureReason.EstimateUnavailable)
        {
            return $"Не удалось оценить размер базы «{databaseName}» (FILEPROPERTY вернул NULL) — " +
                "без оценки бэкап не стартует (защита диска).";
        }

        if (estimate.Reason == BackupFailureReason.BackupFailed)
        {
            // BackupFailed на этом этапе — либо путь не локальный диск, либо диск не найден
            // среди локальных (xp_fixeddrives). Различаем тем же чистым TryGetDriveLetter.
            if (!TryGetDriveLetter(folderRoot, out var drive))
            {
                return $"Папка бэкапов «{folderRoot}» должна указывать на локальный диск SQL-сервера " +
                    "в виде «D:\\Backups» (UNC и относительные пути не поддерживаются).";
            }

            return $"Диск «{drive}:» не найден среди локальных дисков SQL-сервера (xp_fixeddrives) — " +
                "проверка свободного места невозможна.";
        }

        // InsufficientSpace — обе цифры заполнены.
        TryGetDriveLetter(folderRoot, out var spaceDrive);
        var freeMb = (estimate.FreeSpaceBytes ?? 0) / BytesPerMb;
        var estimateMb = (estimate.EstimatedSizeBytes ?? 0) / BytesPerMb;
        var requiredMb = estimateMb + safetyMarginMb;
        return $"Недостаточно места на диске «{spaceDrive}:»: свободно {freeMb} МБ, требуется " +
            $"не менее {requiredMb} МБ (данные базы ≈{estimateMb} МБ + запас {safetyMarginMb} МБ).";
    }

    private static SqlBackupEstimate DegradedEstimate(int safetyMarginMb, BackupFailureReason reason) =>
        new(null, null, (long)safetyMarginMb * BytesPerMb, false, reason);

    private string BuildConnectionString(string server) =>
        new SqlConnectionStringBuilder(_baseConnectionString)
        {
            DataSource = server,
            InitialCatalog = "master",
            ConnectTimeout = ConnectTimeoutSeconds,
        }.ConnectionString;

    // folderRoot обязан быть путём на локальном диске вида «D:\…» — буква нужна для
    // проверки места по xp_fixeddrives.
    internal static bool TryGetDriveLetter(string folderRoot, out char drive)
    {
        drive = default;
        if (folderRoot.Length < 3 || folderRoot[1] != ':' || folderRoot[2] is not ('\\' or '/'))
        {
            return false;
        }

        if (!char.IsAsciiLetter(folderRoot[0]))
        {
            return false;
        }

        drive = char.ToUpperInvariant(folderRoot[0]);
        return true;
    }

    // ── SQL-шаги цикла ──────────────────────────────────────────────────────────────

    private static async Task<bool> IsSysadminAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT IS_SRVROLEMEMBER('sysadmin');";
        command.CommandTimeout = MetadataCommandTimeoutSeconds;
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is int i && i == 1;
    }

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

    // Свободное место диска в МБ из xp_fixeddrives (выводит только локальные диски).
    private static async Task<long?> ReadFreeMbAsync(
        SqlConnection connection, char drive, CancellationToken ct)
    {
        const string sql = @"
CREATE TABLE #drives (drive char(1) PRIMARY KEY, free_mb bigint NOT NULL);
INSERT INTO #drives (drive, free_mb) EXEC master.dbo.xp_fixeddrives;
SELECT free_mb FROM #drives WHERE drive = @drive;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = MetadataCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@drive", drive.ToString());
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is long mb ? mb : null;
    }

    private static async Task CreateSubdirAsync(
        SqlConnection connection, string folder, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "EXEC master.dbo.xp_create_subdir @folder;";
        command.CommandTimeout = MetadataCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@folder", folder);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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

    // xp_delete_file удаляет в папке только распознанные файлы бэкапа (читает заголовок)
    // старше cutoff — чужой файл с расширением .bak не тронет. cutoff — в локальном
    // времени SQL-хоста.
    private static async Task DeleteFilesOlderThanAsync(
        SqlConnection connection, string folder, DateTime cutoffLocal, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "EXEC master.dbo.xp_delete_file 0, @folder, N'BAK', @cutoff;";
        command.CommandTimeout = DeleteCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@folder", folder);
        command.Parameters.AddWithValue("@cutoff", cutoffLocal);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    // xp_fileexist возвращает одну строку с тремя колонками; нас интересует «File Exists»
    // (1 — обычный файл существует, 0 — нет). Путь — всегда строковый параметр (без конкатенации).
    private static async Task<bool> FileExistsAsync(
        SqlConnection connection, string path, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "DECLARE @r TABLE ([File Exists] int, [File is a Directory] int, [Parent Directory Exists] int);" +
            "INSERT INTO @r EXEC master.dbo.xp_fileexist @path;" +
            "SELECT [File Exists] FROM @r;";
        command.CommandTimeout = MetadataCommandTimeoutSeconds;
        command.Parameters.AddWithValue("@path", path);
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is int i && i == 1;
    }

    // Размер сжатого .bak из истории msdb — best-effort: нет строки/сбой чтения → null,
    // бэкап уже состоялся и провалом это не считается.
    private static async Task<long?> TryReadBackupSizeAsync(
        SqlConnection connection, string path, CancellationToken ct)
    {
        const string sql = @"
SELECT TOP (1) bs.compressed_backup_size
FROM msdb.dbo.backupset bs
JOIN msdb.dbo.backupmediafamily mf ON mf.media_set_id = bs.media_set_id
WHERE mf.physical_device_name = @path
ORDER BY bs.backup_finish_date DESC;";

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = MetadataCommandTimeoutSeconds;
            command.Parameters.AddWithValue("@path", path);
            var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            // compressed_backup_size — numeric(20,0) → decimal.
            return value is null or DBNull ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (SqlException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Бэкап БД: операция провалилась (сервер {Server}, база {Database})")]
    private static partial void LogBackupFailed(ILogger logger, string server, string database, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Бэкап БД: удаление устаревших файлов провалилось (сервер {Server}, папка {Folder})")]
    private static partial void LogDeleteFailed(ILogger logger, string server, string folder, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Бэкап БД: проверка наличия файлов на диске провалилась (сервер {Server})")]
    private static partial void LogFilesExistFailed(ILogger logger, string server, Exception ex);
}
