namespace MitLicenseCenter.Application.Performance;

// Состояние DMV-пробы. Ok — данные сняты; PermissionDenied — у учётки backend'а нет права
// VIEW SERVER STATE (prod без sysadmin; нужен GRANT — OPERATIONS «Быстродействие»);
// Unavailable — SQL недоступен/строка не настроена. Degraded-статусы несут пустые списки и
// взводят честный баннер на фронте (паттерн MLC-064a / IIS-permissions). На проводе — строкой
// (JsonStringEnumConverter, Program.cs).
public enum SqlProbeStatus
{
    Ok,
    PermissionDenied,
    Unavailable,
}

// Live-срез нагрузки на MSSQL «1С грузит SQL?» (MLC-068, ADR-26). Pull-по-требованию, НИЧЕГО
// не персистится — собирается на каждый poll, пока вкладка открыта. Measuring=true на первом
// poll'е: wait-stats и IO-stall кумулятивны с момента старта SQL → значимы только как дельта
// между двумя замерами (паттерн CPU%-дельты host-пробы MLC-064); первый раз дельты ещё нет,
// фронт показывает «измеряю…». Активные запросы — мгновенны, доступны сразу. Атрибуция по
// клиенту (database→Infobase→tenant) добавляется эндпоинтом (vertical slice), не пробой —
// см. SqlPerformanceView.
public sealed record SqlPerformanceSnapshot(
    DateTime CapturedAtUtc,
    SqlProbeStatus Status,
    bool Measuring,
    IReadOnlyList<SqlActiveRequest> ActiveRequests,
    IReadOnlyList<SqlDatabaseIo> DatabaseIo,
    IReadOnlyList<SqlWaitDelta> TopWaits)
{
    // Различимые непустые имена баз во всём снимке (активные запросы + IO-stall) — вход для
    // атрибуции по клиенту. Регистронезависимый дедуп: SQL сопоставляет имена баз без учёта
    // регистра (как и Infobase.DatabaseName).
    public IReadOnlyList<string> DatabaseNames()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in ActiveRequests)
        {
            if (!string.IsNullOrWhiteSpace(r.DatabaseName))
            {
                names.Add(r.DatabaseName);
            }
        }

        foreach (var io in DatabaseIo)
        {
            if (!string.IsNullOrWhiteSpace(io.DatabaseName))
            {
                names.Add(io.DatabaseName);
            }
        }

        return names.ToList();
    }
}

// Активный запрос из sys.dm_exec_requests + sys.dm_exec_sessions + sys.dm_exec_sql_text.
// BlockingSessionId — звено цепочки блокировок (≠0 = ждёт другой сеанс; 0 в DMV → null).
// IsOneC=true, когда program_name='1CV83 Server' — готовый признак 1С-originated SQL (атрибуция
// 1С vs не-1С на уровне SQL-сессий, MLC-063); SQL→конкретный сеанс 1С→юзер невозможна (общий
// program_name/host — отклонено в ADR-26). SqlText обрезан (первые ~1000 симв.). Числовые поля
// nullable — DMV отдаёт NULL для неактивных частей (например wait-type у running).
public sealed record SqlActiveRequest(
    int SessionId,
    int? BlockingSessionId,
    string? DatabaseName,
    bool IsOneC,
    string? ProgramName,
    string? HostName,
    string Status,
    string? WaitType,
    long? WaitTimeMs,
    long? CpuTimeMs,
    long? ElapsedMs,
    long? LogicalReads,
    string? SqlText);

// IO-stall по базе данных за интервал между двумя poll'ами (дельта sys.dm_io_virtual_file_stats,
// суммарно по всем файлам базы). Stall — суммарное время ожидания дисковых операций (мс): прямой
// сигнал «база упирается в диск». ReadsDelta/WritesDelta — число операций в интервале (контекст
// для stall). DatabaseName = DB_NAME(database_id); null для отсоединённых баз.
public sealed record SqlDatabaseIo(
    string? DatabaseName,
    long ReadStallMsDelta,
    long WriteStallMsDelta,
    long ReadsDelta,
    long WritesDelta);

// Дельта одного типа ожидания за интервал (sys.dm_os_wait_stats кумулятивна с старта SQL).
// WaitTimeMsDelta — прирост суммарного времени ожидания этого типа; WaitingTasksDelta — прирост
// числа задач, ждавших его. Доброкачественные «холостые» ожидания (sleep/idle) отфильтрованы в
// пробе — остаётся то, что отражает реальную конкуренцию за ресурсы.
public sealed record SqlWaitDelta(
    string WaitType,
    long WaitTimeMsDelta,
    long WaitingTasksDelta);
