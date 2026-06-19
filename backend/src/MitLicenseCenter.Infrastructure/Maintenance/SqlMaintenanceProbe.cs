using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Maintenance;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Maintenance;

// Реальный адаптер пробы обслуживания SQL (MLC-216, ADR-54): live-read свежести резервных
// копий пользовательских баз из msdb.dbo.backupset — БЕЗ собственных таблиц/миграций/джоб
// (вкладка «Обслуживание» раздела «Сервер» — только чтение). Один коннект к master, один
// запрос: последний бэкап КАЖДОГО типа (D/I/L) по каждой пользовательской базе, плюс флаг
// «устарел» считает чистая BackupFreshnessPolicy.
//
// Запрос вокруг backup_finish_date — тот же источник, что TryReadBackupSizeAsync в
// SqlBackupAdapter (msdb.dbo.backupset); фича on-demand бэкапа (COPY_ONLY) этим НЕ затрагивается.
// type в backupset: 'D' = full, 'I' = differential, 'L' = log (прочие — copy-only/file и т.п.,
// здесь не классифицируем). database_name backupset фильтруем по существующим пользовательским
// базам (sys.databases, database_id > 4) — история мог содержать удалённые/системные базы.
//
// Сервер — настройка Sql.Server (единственный источник, single-host ADR-28); параметры
// аутентификации наследуются из ConnectionStrings:Default (как DatabaseSizeProbe/
// SqlBackupAdapter) — меняем только сервер и каталог (master). Таймауты Connect 15s /
// Command 30s — как DatabaseSizeProbe (msdb может быть медленным при большой истории).
//
// Права: чтение msdb.dbo.backupset — проверяем HAS_PERMS_BY_NAME (паттерн HasViewServerStateAsync
// MLC-068). Нет права → Status=PermissionDenied (честный degraded-сигнал, а не пустое «всё
// свежо»). SQL недоступен / строка не настроена → Status=Unavailable. «Never throws» —
// инфраструктурный сбой деградирует в статус; отмена (OperationCanceledException) пробрасывается.
//
// Чистый ADO.NET (как DatabaseSizeProbe/SqlPerformanceProbe) — НЕ Windows-only, без
// [SupportedOSPlatform]/CA1416. Stateless → singleton.
//
// Структура расширяема: MLC-217 (планы обслуживания sysmaintplan_* + SQL Agent) дорастит ЭТУ
// же пробу — новый метод/поля снимка, без слома контракта свежести бэкапов.
internal sealed partial class SqlMaintenanceProbe : IMaintenanceProbe
{
    private const int ConnectTimeoutSeconds = 15;
    private const int CommandTimeoutSeconds = 30;

    private readonly string? _baseConnectionString;
    private readonly ISettingsSnapshot _settings;
    private readonly TimeProvider _clock;
    private readonly ILogger<SqlMaintenanceProbe> _logger;

    public SqlMaintenanceProbe(
        IConfiguration configuration,
        ISettingsSnapshot settings,
        TimeProvider clock,
        ILogger<SqlMaintenanceProbe> logger)
    {
        _baseConnectionString = configuration.GetConnectionString("Default");
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BackupFreshnessSnapshot> GetBackupFreshnessAsync(CancellationToken ct)
    {
        var server = _settings.GetString(SettingKey.SqlServer);
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return Degraded(MaintenanceProbeStatus.Unavailable);
        }

        var nowUtc = _clock.GetUtcNow().UtcDateTime;

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString(server));
            await connection.OpenAsync(ct).ConfigureAwait(false);

            if (!await HasReadBackupsetAsync(connection, ct).ConfigureAwait(false))
            {
                return Degraded(MaintenanceProbeStatus.PermissionDenied);
            }

            var rows = await ReadLatestBackupsAsync(connection, ct).ConfigureAwait(false);

            var databases = new List<DatabaseBackupFreshness>(rows.Count);
            foreach (var row in rows)
            {
                databases.Add(new DatabaseBackupFreshness(
                    row.DatabaseName,
                    row.LastFullUtc,
                    row.LastDiffUtc,
                    row.LastLogUtc,
                    BackupFreshnessPolicy.IsStale(row.LastFullUtc, nowUtc)));
            }

            return new BackupFreshnessSnapshot(MaintenanceProbeStatus.Ok, databases);
        }
        catch (SqlException ex)
        {
            LogProbeUnavailable(_logger, server, ex);
            return Degraded(MaintenanceProbeStatus.Unavailable);
        }
        catch (InvalidOperationException ex)
        {
            // Битая строка подключения / соединение умерло посреди операции.
            LogProbeUnavailable(_logger, server, ex);
            return Degraded(MaintenanceProbeStatus.Unavailable);
        }
    }

    private static BackupFreshnessSnapshot Degraded(MaintenanceProbeStatus status) =>
        new(status, []);

    // Наследует параметры из ConnectionStrings:Default; подменяет сервер и каталог (master),
    // как DatabaseSizeProbe/SqlBackupAdapter (приватная логика повторена — существующие
    // адаптеры не трогаем).
    private string BuildConnectionString(string server) =>
        new SqlConnectionStringBuilder(_baseConnectionString)
        {
            DataSource = server,
            InitialCatalog = "master",
            ConnectTimeout = ConnectTimeoutSeconds,
        }.ConnectionString;

    // Право на чтение истории бэкапов: SELECT на msdb.dbo.backupset. Нет права → честный
    // PermissionDenied вместо пустого «всё свежо» (паттерн HasViewServerStateAsync MLC-068).
    private static async Task<bool> HasReadBackupsetAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT HAS_PERMS_BY_NAME('msdb.dbo.backupset', 'OBJECT', 'SELECT');";
        command.CommandTimeout = CommandTimeoutSeconds;
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is int i && i == 1;
    }

    // Последний бэкап каждого типа (D/I/L) по каждой пользовательской базе. Свёртка по типу —
    // через MAX(backup_finish_date) с PIVOT-подобной CASE-агрегацией; backup_finish_date в
    // backupset хранится в ЛОКАЛЬНОМ времени SQL-хоста, конвертируем в UTC при чтении (single-host
    // ADR-28: панель и SQL co-located, TimeZoneInfo.Local совпадает). JOIN к sys.databases
    // отбрасывает историю удалённых/системных баз (database_id > 4). База без FULL-бэкапа всё
    // равно попадает в результат, если у неё есть строки backupset любого типа.
    private static async Task<IReadOnlyList<BackupRow>> ReadLatestBackupsAsync(
        SqlConnection connection, CancellationToken ct)
    {
        const string sql = @"
SELECT
    d.name AS DbName,
    MAX(CASE WHEN bs.type = 'D' THEN bs.backup_finish_date END) AS LastFull,
    MAX(CASE WHEN bs.type = 'I' THEN bs.backup_finish_date END) AS LastDiff,
    MAX(CASE WHEN bs.type = 'L' THEN bs.backup_finish_date END) AS LastLog
FROM sys.databases d
JOIN msdb.dbo.backupset bs ON bs.database_name = d.name
WHERE d.database_id > 4
  AND bs.type IN ('D', 'I', 'L')
GROUP BY d.name;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;

        var rows = new List<BackupRow>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new BackupRow(
                DatabaseName: reader.GetString(0),
                LastFullUtc: await ReadLocalAsUtcAsync(reader, 1, ct).ConfigureAwait(false),
                LastDiffUtc: await ReadLocalAsUtcAsync(reader, 2, ct).ConfigureAwait(false),
                LastLogUtc: await ReadLocalAsUtcAsync(reader, 3, ct).ConfigureAwait(false)));
        }

        return rows;
    }

    // backup_finish_date (datetime) — локальное время SQL-хоста; читаем как Unspecified и
    // конвертируем в UTC (single-host: TimeZoneInfo.Local панели = SQL-хоста). NULL → null.
    private static async Task<DateTime?> ReadLocalAsUtcAsync(
        SqlDataReader reader, int ordinal, CancellationToken ct)
    {
        if (await reader.IsDBNullAsync(ordinal, ct).ConfigureAwait(false))
        {
            return null;
        }

        var local = DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.Local);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Обслуживание: проба свежести бэкапов недоступна (сервер {Server})")]
    private static partial void LogProbeUnavailable(ILogger logger, string server, Exception ex);

    // Сырая строка backupset-агрегации (последний бэкап каждого типа по базе).
    private readonly record struct BackupRow(
        string DatabaseName, DateTime? LastFullUtc, DateTime? LastDiffUtc, DateTime? LastLogUtc);
}
