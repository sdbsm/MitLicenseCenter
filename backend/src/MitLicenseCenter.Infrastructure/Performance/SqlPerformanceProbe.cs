using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Performance;

namespace MitLicenseCenter.Infrastructure.Performance;

// Реальный адаптер DMV-метрик MSSQL (MLC-068, ADR-26, Фаза 3). Отвечает на «1С грузит SQL?»:
//   • активные запросы + цепочки блокировок — sys.dm_exec_requests ⨝ sys.dm_exec_sessions
//     OUTER APPLY sys.dm_exec_sql_text (текст запроса);
//   • IO-stall по базам — sys.dm_io_virtual_file_stats (дельта между poll'ами);
//   • wait-stats — sys.dm_os_wait_stats (кумулятивны → дельта, как CPU% host-пробы MLC-064).
//
// Подключение наследует параметры из ConnectionStrings:Default (как SqlDatabaseDiscovery) —
// та же учётка, что у панели; InitialCatalog=master (DMV серверного охвата). Топология
// single-node co-located (ADR-26): SQL панели и SQL инфобаз 1С — один инстанс, поэтому
// DataSource строки панели = нужный сервер. Признак 1С-SQL — program_name='1CV83 Server'
// (MLC-063); атрибуция по базе делается эндпоинтом, не здесь (SQL→сеанс→юзер невозможна).
//
// Права: нужен VIEW SERVER STATE. Нет права → Status=PermissionDenied (честный degraded-сигнал,
// как баннер MLC-064a), не пустой «всё спокойно». SQL недоступен/строка не настроена →
// Status=Unavailable. «Never throws» — инфраструктурный сбой деградирует в статус.
//
// НЕ Windows-only: чистый ADO.NET (как SqlDatabaseDiscovery) — компилируется кроссплатформенно,
// атрибута [SupportedOSPlatform] и #pragma CA1416 не требует. Singleton — держит предыдущий
// срез wait/IO для дельты между poll'ами (паттерн ColdThrottleState/host-пробы); первый poll
// → Measuring=true (дельт ещё нет). Сами DMV-чтения вынесены за read-блокировку, под lock —
// только swap прошлого среза и чистый расчёт дельт.
internal sealed partial class SqlPerformanceProbe : ISqlPerformanceProbe
{
    private const int ConnectTimeoutSeconds = 5;
    private const int CommandTimeoutSeconds = 5;
    private const int TopActiveRequests = 50;
    private const int TopWaits = 15;
    private const int SqlTextMaxChars = 1000;

    private readonly string? _baseConnectionString;
    private readonly TimeProvider _clock;
    private readonly ILogger<SqlPerformanceProbe> _logger;

    private readonly object _gate = new();
    private IReadOnlyDictionary<string, WaitRaw>? _previousWaits;       // wait_type → срез
    private IReadOnlyDictionary<int, IoRaw>? _previousIo;               // database_id → counters

    public SqlPerformanceProbe(
        IConfiguration configuration, TimeProvider clock, ILogger<SqlPerformanceProbe> logger)
    {
        _baseConnectionString = configuration.GetConnectionString("Default");
        _clock = clock;
        _logger = logger;
    }

    public async Task<SqlPerformanceSnapshot> CaptureAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        if (string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return Degraded(now, SqlProbeStatus.Unavailable);
        }

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString());
            await connection.OpenAsync(ct).ConfigureAwait(false);

            if (!await HasViewServerStateAsync(connection, ct).ConfigureAwait(false))
            {
                return Degraded(now, SqlProbeStatus.PermissionDenied);
            }

            var requests = await ReadActiveRequestsAsync(connection, ct).ConfigureAwait(false);
            var waitsRaw = await ReadWaitsAsync(connection, ct).ConfigureAwait(false);
            var ioRaw = await ReadIoAsync(connection, ct).ConfigureAwait(false);

            lock (_gate)
            {
                var measuring = _previousWaits is null || _previousIo is null;

                var waitDeltas = measuring
                    ? Array.Empty<SqlWaitDelta>()
                    : ComputeWaitDeltas(_previousWaits!, waitsRaw, TopWaits);
                var ioDeltas = measuring
                    ? Array.Empty<SqlDatabaseIo>()
                    : ComputeIoDeltas(_previousIo!, ioRaw);

                _previousWaits = waitsRaw.ToDictionary(w => w.WaitType, w => w, StringComparer.Ordinal);
                _previousIo = ioRaw.ToDictionary(i => i.DatabaseId);

                return new SqlPerformanceSnapshot(
                    now, SqlProbeStatus.Ok, measuring, requests, ioDeltas, waitDeltas);
            }
        }
        catch (SqlException ex)
        {
            LogSqlUnavailable(_logger, ex);
            return Degraded(now, SqlProbeStatus.Unavailable);
        }
        catch (InvalidOperationException ex)
        {
            // Битая строка подключения / отсутствующий провайдер — не валим live-снимок.
            LogSqlUnavailable(_logger, ex);
            return Degraded(now, SqlProbeStatus.Unavailable);
        }
    }

    private static SqlPerformanceSnapshot Degraded(DateTime now, SqlProbeStatus status) =>
        new(now, status, Measuring: false, [], [], []);

    private string BuildConnectionString() =>
        new SqlConnectionStringBuilder(_baseConnectionString)
        {
            InitialCatalog = "master", // DMV серверного охвата; не зависит от текущей базы
            ConnectTimeout = ConnectTimeoutSeconds,
        }.ConnectionString;

    // ── DMV-чтения (вне lock) ───────────────────────────────────────────────────────

    private static async Task<bool> HasViewServerStateAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW SERVER STATE');";
        command.CommandTimeout = CommandTimeoutSeconds;
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is int i && i == 1;
    }

    private static async Task<IReadOnlyList<SqlActiveRequest>> ReadActiveRequestsAsync(
        SqlConnection connection, CancellationToken ct)
    {
        // Только пользовательские сеансы (is_user_process=1), кроме собственного запроса пробы
        // (@@SPID). Топ по cpu_time — самые «дорогие» активные запросы. OUTER APPLY к sql_text —
        // NULL при пустом sql_handle (запрос без текста). blocking_session_id 0 в DMV → не блокирован.
        // Типы DMV: session_id / blocking_session_id = smallint; wait_time / cpu_time /
        // total_elapsed_time = int; logical_reads = bigint.
        const string sql = @"
SELECT TOP (@top)
    r.session_id,
    r.blocking_session_id,
    DB_NAME(r.database_id)       AS database_name,
    s.program_name,
    s.host_name,
    r.status,
    r.wait_type,
    r.wait_time,
    r.cpu_time,
    r.total_elapsed_time,
    r.logical_reads,
    SUBSTRING(t.text, 1, @textLen) AS sql_text
FROM sys.dm_exec_requests r
JOIN sys.dm_exec_sessions s ON s.session_id = r.session_id
OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE s.is_user_process = 1
  AND r.session_id <> @@SPID
ORDER BY r.cpu_time DESC;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;
        command.Parameters.AddWithValue("@top", TopActiveRequests);
        command.Parameters.AddWithValue("@textLen", SqlTextMaxChars);

        var requests = new List<SqlActiveRequest>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var program = GetNullableString(reader, 3);
            var blocking = GetNullableInt16(reader, 1);
            requests.Add(new SqlActiveRequest(
                SessionId: reader.GetInt16(0),
                BlockingSessionId: blocking is 0 or null ? null : blocking,
                DatabaseName: GetNullableString(reader, 2),
                IsOneC: IsOneCProgram(program),
                ProgramName: program,
                HostName: GetNullableString(reader, 4),
                Status: GetNullableString(reader, 5) ?? string.Empty,
                WaitType: GetNullableString(reader, 6),
                WaitTimeMs: GetNullableInt32(reader, 7),
                CpuTimeMs: GetNullableInt32(reader, 8),
                ElapsedMs: GetNullableInt32(reader, 9),
                LogicalReads: GetNullableInt64(reader, 10),
                SqlText: GetNullableString(reader, 11)));
        }

        return requests;
    }

    private static async Task<IReadOnlyList<WaitRaw>> ReadWaitsAsync(SqlConnection connection, CancellationToken ct)
    {
        const string sql = @"
SELECT wait_type, wait_time_ms, waiting_tasks_count
FROM sys.dm_os_wait_stats
WHERE wait_time_ms > 0;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;

        var waits = new List<WaitRaw>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            waits.Add(new WaitRaw(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2)));
        }

        return waits;
    }

    private static async Task<IReadOnlyList<IoRaw>> ReadIoAsync(SqlConnection connection, CancellationToken ct)
    {
        const string sql = @"
SELECT
    vfs.database_id,
    DB_NAME(vfs.database_id)   AS database_name,
    SUM(vfs.num_of_reads)      AS num_reads,
    SUM(vfs.num_of_writes)     AS num_writes,
    SUM(vfs.io_stall_read_ms)  AS io_stall_read_ms,
    SUM(vfs.io_stall_write_ms) AS io_stall_write_ms
FROM sys.dm_io_virtual_file_stats(NULL, NULL) vfs
GROUP BY vfs.database_id;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;

        var io = new List<IoRaw>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            io.Add(new IoRaw(
                DatabaseId: reader.GetInt16(0), // database_id = smallint
                DatabaseName: GetNullableString(reader, 1),
                Reads: reader.GetInt64(2),
                Writes: reader.GetInt64(3),
                ReadStallMs: reader.GetInt64(4),
                WriteStallMs: reader.GetInt64(5)));
        }

        return io;
    }

    // ── Чистый расчёт дельт (тестируется без БД) ─────────────────────────────────────

    // Дельта wait-stats: прирост wait_time_ms по типу ожидания между двумя срезами, минус
    // доброкачественные «холостые» ожидания (sleep/idle/broker — не отражают конкуренцию за
    // ресурс). Отрицательная дельта (рестарт SQL обнулил счётчики) → пропуск. Топ по приросту.
    internal static IReadOnlyList<SqlWaitDelta> ComputeWaitDeltas(
        IReadOnlyDictionary<string, WaitRaw> previous, IReadOnlyList<WaitRaw> current, int top)
    {
        var deltas = new List<SqlWaitDelta>();
        foreach (var w in current)
        {
            if (BenignWaitTypes.Contains(w.WaitType))
            {
                continue;
            }

            if (!previous.TryGetValue(w.WaitType, out var before))
            {
                continue; // тип появился между срезами — без базы для дельты
            }

            var deltaMs = w.WaitTimeMs - before.WaitTimeMs;
            if (deltaMs <= 0)
            {
                continue;
            }

            deltas.Add(new SqlWaitDelta(
                w.WaitType, deltaMs, Math.Max(0, w.WaitingTasksCount - before.WaitingTasksCount)));
        }

        return deltas
            .OrderByDescending(d => d.WaitTimeMsDelta)
            .Take(top)
            .ToList();
    }

    // Дельта IO-stall по базе: прирост суммарного stall и числа операций между срезами.
    // База появилась/рестарт счётчиков (отрицательная дельта) → пропуск. Только базы с
    // ненулевой активностью в интервале (stall или операции выросли).
    internal static IReadOnlyList<SqlDatabaseIo> ComputeIoDeltas(
        IReadOnlyDictionary<int, IoRaw> previous, IReadOnlyList<IoRaw> current)
    {
        var deltas = new List<SqlDatabaseIo>();
        foreach (var io in current)
        {
            if (!previous.TryGetValue(io.DatabaseId, out var before))
            {
                continue;
            }

            var readStall = io.ReadStallMs - before.ReadStallMs;
            var writeStall = io.WriteStallMs - before.WriteStallMs;
            var reads = io.Reads - before.Reads;
            var writes = io.Writes - before.Writes;

            if (readStall <= 0 && writeStall <= 0 && reads <= 0 && writes <= 0)
            {
                continue;
            }

            deltas.Add(new SqlDatabaseIo(
                io.DatabaseName,
                Math.Max(0, readStall),
                Math.Max(0, writeStall),
                Math.Max(0, reads),
                Math.Max(0, writes)));
        }

        return deltas
            .OrderByDescending(d => d.ReadStallMsDelta + d.WriteStallMsDelta)
            .ToList();
    }

    internal static bool IsOneCProgram(string? programName) =>
        string.Equals(programName?.Trim(), OneCProgramName, StringComparison.OrdinalIgnoreCase);

    // Признак 1С-originated SQL: 1С-сервер кластера подключается к MSSQL под этим именем
    // приложения (подтверждено разведкой MLC-063).
    private const string OneCProgramName = "1CV83 Server";

    // Доброкачественные «холостые» ожидания — фоновые/idle, не отражают конкуренцию за ресурсы;
    // классический список для анализа wait-stats. Исключаем, чтобы дельта показывала значимое.
    private static readonly HashSet<string> BenignWaitTypes = new(StringComparer.Ordinal)
    {
        "SLEEP_TASK", "BROKER_TASK_STOP", "BROKER_TO_FLUSH", "BROKER_EVENTHANDLER",
        "BROKER_RECEIVE_WAITFOR", "BROKER_TRANSMITTER", "CHECKPOINT_QUEUE",
        "CLR_AUTO_EVENT", "CLR_MANUAL_EVENT", "DBMIRROR_DBM_EVENT", "DBMIRROR_EVENTS_QUEUE",
        "DBMIRRORING_CMD", "DIRTY_PAGE_POLL", "DISPATCHER_QUEUE_SEMAPHORE",
        "FT_IFTS_SCHEDULER_IDLE_WAIT", "FT_IFTSHC_MUTEX", "HADR_CLUSAPI_CALL",
        "HADR_FILESTREAM_IOMGR_IOCOMPLETION", "HADR_LOGCAPTURE_WAIT", "HADR_NOTIFICATION_DEQUEUE",
        "HADR_TIMER_TASK", "HADR_WORK_QUEUE", "KSOURCE_WAKEUP", "LAZYWRITER_SLEEP",
        "LOGMGR_QUEUE", "MEMORY_ALLOCATION_EXT", "ONDEMAND_TASK_QUEUE", "PARALLEL_REDO_DRAIN_WORKER",
        "PARALLEL_REDO_LOG_CACHE", "PARALLEL_REDO_TRAN_LIST", "PARALLEL_REDO_WORKER_SYNC",
        "PARALLEL_REDO_WORKER_WAIT_WORK", "PREEMPTIVE_XE_GETTARGETSTATE",
        "PWAIT_ALL_COMPONENTS_INITIALIZED", "PWAIT_DIRECTLOGCONSUMER_GETNEXT",
        "QDS_PERSIST_TASK_MAIN_LOOP_SLEEP", "QDS_ASYNC_QUEUE", "QDS_CLEANUP_STALE_QUERIES_TASK_MAIN_LOOP_SLEEP",
        "QDS_SHUTDOWN_QUEUE", "REDO_THREAD_PENDING_WORK", "REQUEST_FOR_DEADLOCK_SEARCH",
        "RESOURCE_QUEUE", "SERVER_IDLE_CHECK", "SLEEP_BPOOL_FLUSH", "SLEEP_DBSTARTUP",
        "SLEEP_DCOMSTARTUP", "SLEEP_MASTERDBREADY", "SLEEP_MASTERMDREADY", "SLEEP_MASTERUPGRADED",
        "SLEEP_MSDBSTARTUP", "SLEEP_SYSTEMTASK", "SP_SERVER_DIAGNOSTICS_SLEEP", "SQLTRACE_BUFFER_FLUSH",
        "SQLTRACE_INCREMENTAL_FLUSH_SLEEP", "SQLTRACE_WAIT_ENTRIES", "WAIT_FOR_RESULTS",
        "WAITFOR", "WAIT_XTP_HOST_WAIT", "WAIT_XTP_OFFLINE_CKPT_NEW_LOG", "WAIT_XTP_CKPT_CLOSE",
        "XE_DISPATCHER_JOIN", "XE_DISPATCHER_WAIT", "XE_TIMER_EVENT",
    };

    // ── Чтение nullable-колонок ──────────────────────────────────────────────────────

    private static string? GetNullableString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static short? GetNullableInt16(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt16(ordinal);

    private static int? GetNullableInt32(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static long? GetNullableInt64(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SQL-метрики: DMV-проба недоступна (раздел «Быстродействие»)")]
    private static partial void LogSqlUnavailable(ILogger logger, Exception ex);

    // Сырой срез wait-stats (для дельты между poll'ами).
    internal readonly record struct WaitRaw(string WaitType, long WaitTimeMs, long WaitingTasksCount);

    // Сырой срез IO по базе (агрегат по файлам; для дельты между poll'ами).
    internal readonly record struct IoRaw(
        int DatabaseId, string? DatabaseName, long Reads, long Writes, long ReadStallMs, long WriteStallMs);
}
