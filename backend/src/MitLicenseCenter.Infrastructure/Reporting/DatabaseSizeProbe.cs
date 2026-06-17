using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Reporting;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Reporting;

// Реальный адаптер замера размеров баз SQL (MLC-185). Один коннект к master, один запрос
// к sys.master_files — выделенное (allocated) место всех пользовательских баз
// (database_id > 4), сгруппированное по базе и типу файла (ROWS/LOG). `size` в
// sys.master_files — в страницах по 8 КБ → байты = Pages * 8 * 1024. Свёртка по DbName:
// ROWS → DataBytes, LOG → LogBytes (база может иметь несколько файлов каждого типа —
// SUM в запросе агрегирует по типу, здесь складываем типы в одну строку). База без
// LOG-строки → LogBytes=0.
//
// Сервер — настройка Sql.Server (единственный источник, single-host ADR-28). Параметры
// аутентификации (Trusted_Connection / TrustServerCertificate / Encrypt) наследуются из
// ConnectionStrings:Default — меняются только сервер и каталог (master), как в
// SqlDatabaseDiscovery / SqlBackupAdapter. Чистый ADO.NET → НЕ Windows-only, без
// [SupportedOSPlatform]/CA1416. Stateless → singleton.
//
// «Never throws» (как SqlBackupAdapter): нет строки подключения / SQL недоступен →
// ПУСТОЙ список (деградированный результат); отмена (OperationCanceledException)
// пробрасывается.
internal sealed partial class DatabaseSizeProbe : IDatabaseSizeProbe
{
    // size в sys.master_files — в страницах по 8 КБ.
    private const long BytesPerPage = 8L * 1024L;
    private const int ConnectTimeoutSeconds = 15;
    private const int CommandTimeoutSeconds = 30;

    private readonly string? _baseConnectionString;
    private readonly ISettingsSnapshot _settings;
    private readonly ILogger<DatabaseSizeProbe> _logger;

    public DatabaseSizeProbe(
        IConfiguration configuration, ISettingsSnapshot settings, ILogger<DatabaseSizeProbe> logger)
    {
        _baseConnectionString = configuration.GetConnectionString("Default");
        _settings = settings;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DatabaseSizeReading>> ReadSizesAsync(CancellationToken ct)
    {
        var server = _settings.GetString(SettingKey.SqlServer);
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return [];
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString(server));
            await connection.OpenAsync(ct).ConfigureAwait(false);

            // database_id > 4 исключает системные master/tempdb/model/msdb (как в
            // SqlDatabaseDiscovery). SUM агрегирует несколько файлов одного типа.
            const string sql = @"
SELECT DB_NAME(database_id) AS DbName, type_desc AS TypeDesc, SUM(CAST(size AS bigint)) AS Pages
FROM sys.master_files
WHERE database_id > 4
GROUP BY database_id, type_desc;";

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = CommandTimeoutSeconds;

            // Свёртка двух строк (ROWS/LOG) одной базы в одно показание.
            var byName = new Dictionary<string, (long Data, long Log)>(StringComparer.Ordinal);

            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                // DB_NAME может вернуть NULL для базы, исчезнувшей между чтением каталога
                // и проекцией — пропускаем (защитно).
                if (await reader.IsDBNullAsync(0, ct).ConfigureAwait(false))
                {
                    continue;
                }

                var name = reader.GetString(0);
                var typeDesc = reader.GetString(1);
                var bytes = reader.GetInt64(2) * BytesPerPage;

                byName.TryGetValue(name, out var acc);
                if (string.Equals(typeDesc, "ROWS", StringComparison.Ordinal))
                {
                    acc.Data += bytes;
                }
                else if (string.Equals(typeDesc, "LOG", StringComparison.Ordinal))
                {
                    acc.Log += bytes;
                }
                // FILESTREAM / FULLTEXT и прочие type_desc игнорируем — отчёт о размере
                // оперирует данными и логом (как DatabaseSizeSnapshot).

                byName[name] = acc;
            }

            var readings = new List<DatabaseSizeReading>(byName.Count);
            foreach (var (name, acc) in byName)
            {
                readings.Add(new DatabaseSizeReading(name, acc.Data, acc.Log));
            }

            return readings;
        }
        catch (SqlException ex)
        {
            LogReadFailed(_logger, server, ex);
            return [];
        }
        catch (InvalidOperationException ex)
        {
            // Битая строка подключения / соединение умерло посреди операции.
            LogReadFailed(_logger, server, ex);
            return [];
        }
    }

    // Наследует параметры из ConnectionStrings:Default; подменяет сервер и каталог
    // (master), как BuildConnectionString в SqlBackupAdapter (приватный там → логика
    // повторена здесь, существующий адаптер не трогаем).
    private string BuildConnectionString(string server) =>
        new SqlConnectionStringBuilder(_baseConnectionString)
        {
            DataSource = server,
            InitialCatalog = "master",
            ConnectTimeout = ConnectTimeoutSeconds,
        }.ConnectionString;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Размеры БД: замер провалился (сервер {Server})")]
    private static partial void LogReadFailed(ILogger logger, string server, Exception ex);
}
