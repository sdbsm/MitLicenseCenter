using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MitLicenseCenter.Application.Maintenance;
using MitLicenseCenter.Application.Settings;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Infrastructure.Maintenance;

// Планы обслуживания SQL (MLC-217, ADR-54): live-read msdb.dbo.sysmaintplan_plans/
// sysmaintplan_subplans (планы и под-планы; под-план привязан к заданию SQL Agent по job_id) +
// история заданий (sysjobs/sysjobhistory) для последнего прогона + детализация по шагам из
// sysmaintplan_log/sysmaintplan_logdetail (что именно упало). БЕЗ собственных таблиц/миграций/
// джоб — только чтение (продолжение пробы свежести бэкапов, та же строка подключения/таймауты).
//
// Различение «по расписанию» vs «по запросу»: под-план «по расписанию» — у его задания есть
// ВКЛЮЧЁННОЕ расписание (sysschedules.enabled=1 через sysjobschedules); под-план без такого —
// ручной (владелец держит ручные под-планы «перестроение индекса»/«month»). Классификация
// (успех/провал/просрочен/норма) — чистая SubplanRunPolicy (тестируется без SQL); просрочка
// (Overdue) считается ТОЛЬКО для под-планов С расписанием.
//
// Паттерн как у свежести бэкапов: never-throws + HAS_PERMS_BY_NAME. Деградация статусом:
//   нет прав на msdb.dbo.sysmaintplan_plans → PermissionDenied;
//   SQL Agent отсутствует/остановлен (Express) или нет доступа к sysjobs/sysjobhistory →
//     AgentUnavailable (честный «агент недоступен», не ошибка — план есть, истории прогонов нет);
//   SQL недоступен / строка не настроена → Unavailable.
// Эндпоинт не 500-ит; отмена (OperationCanceledException) пробрасывается.
internal sealed partial class SqlMaintenanceProbe
{
    public async Task<MaintenancePlansSnapshot> GetMaintenancePlansAsync(CancellationToken ct)
    {
        var server = _settings.GetString(SettingKey.SqlServer);
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(_baseConnectionString))
        {
            return PlansDegraded(MaintenancePlansStatus.Unavailable);
        }

        var nowUtc = _clock.GetUtcNow().UtcDateTime;

        try
        {
            await using var connection = new SqlConnection(BuildConnectionString(server));
            await connection.OpenAsync(ct).ConfigureAwait(false);

            if (!await HasReadMaintplanAsync(connection, ct).ConfigureAwait(false))
            {
                return PlansDegraded(MaintenancePlansStatus.PermissionDenied);
            }

            // SQL Agent может отсутствовать (Express) или не давать доступа к истории заданий —
            // тогда честный AgentUnavailable (планы прочитать смогли, прогоны — нет).
            if (!await HasReadJobHistoryAsync(connection, ct).ConfigureAwait(false))
            {
                return PlansDegraded(MaintenancePlansStatus.AgentUnavailable);
            }

            var subplanRows = await ReadSubplansAsync(connection, ct).ConfigureAwait(false);
            var detailRows = await ReadLastRunDetailsAsync(connection, ct).ConfigureAwait(false);

            return BuildSnapshot(subplanRows, detailRows, nowUtc);
        }
        catch (SqlException ex)
        {
            LogPlansUnavailable(_logger, server, ex);
            return PlansDegraded(MaintenancePlansStatus.Unavailable);
        }
        catch (InvalidOperationException ex)
        {
            LogPlansUnavailable(_logger, server, ex);
            return PlansDegraded(MaintenancePlansStatus.Unavailable);
        }
    }

    private static MaintenancePlansSnapshot PlansDegraded(MaintenancePlansStatus status) =>
        new(status, []);

    // Сборка снимка из сырых строк: группируем под-планы по плану, классифицируем каждый
    // под-план чистой SubplanRunPolicy, подвешиваем детализацию шагов последнего прогона.
    private static MaintenancePlansSnapshot BuildSnapshot(
        IReadOnlyList<SubplanRow> subplans,
        IReadOnlyList<DetailRow> details,
        DateTime nowUtc)
    {
        // Детализация шагов по subplan_id (последний прогон каждого под-плана).
        var detailsBySubplan = details
            .GroupBy(d => d.SubplanId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MaintenanceTaskDetail>)g
                    .OrderBy(d => d.StartUtc ?? DateTime.MaxValue)
                    .Select(d => new MaintenanceTaskDetail(d.Detail, d.Succeeded))
                    .ToList());

        var plans = subplans
            .GroupBy(s => s.PlanName, StringComparer.Ordinal)
            .Select(planGroup => new MaintenancePlan(
                planGroup.Key,
                planGroup
                    .Select(s => new MaintenanceSubplan(
                        s.SubplanName,
                        s.HasSchedule,
                        SubplanRunPolicy.Classify(s.HasSchedule, s.LastSucceeded, s.LastRunUtc, nowUtc),
                        s.LastRunUtc,
                        s.DurationSeconds,
                        detailsBySubplan.TryGetValue(s.SubplanId, out var tasks) ? tasks : []))
                    .ToList()))
            .ToList();

        return new MaintenancePlansSnapshot(MaintenancePlansStatus.Ok, plans);
    }

    // Право на чтение истории планов: SELECT на msdb.dbo.sysmaintplan_plans.
    private static async Task<bool> HasReadMaintplanAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT HAS_PERMS_BY_NAME('msdb.dbo.sysmaintplan_plans', 'OBJECT', 'SELECT');";
        command.CommandTimeout = CommandTimeoutSeconds;
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is int i && i == 1;
    }

    // Право на историю заданий SQL Agent: SELECT на msdb.dbo.sysjobhistory. На Express SQL Agent
    // нет — таблицы msdb.dbo.sysjob* существуют, но без агента/прав вернёт 0 → AgentUnavailable.
    private static async Task<bool> HasReadJobHistoryAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT HAS_PERMS_BY_NAME('msdb.dbo.sysjobhistory', 'OBJECT', 'SELECT');";
        command.CommandTimeout = CommandTimeoutSeconds;
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is int i && i == 1;
    }

    // Под-планы со связанным заданием, флагом «есть включённое расписание» и последним прогоном.
    // Последний прогон берём из sysmaintplan_log (start_time/end_time + succeeded) — он точнее
    // отражает прогоны именно под-плана (одно задание может нести несколько под-планов). run_status
    // sysjobhistory не разбираем (план = под-планы лога). Время лога — UTC уже? Нет: start_time/
    // end_time в sysmaintplan_log — datetime в локале SQL-хоста, конвертируем в UTC (single-host).
    private async Task<IReadOnlyList<SubplanRow>> ReadSubplansAsync(SqlConnection connection, CancellationToken ct)
    {
        // last_run = последняя запись лога под-плана; has_schedule = у задания есть включённое
        // расписание (sysjobschedules → sysschedules.enabled=1). OUTER APPLY к последнему логу,
        // чтобы под-план без прогонов всё равно вернулся (LastRun=NULL).
        const string sql = @"
SELECT
    p.name AS PlanName,
    sp.subplan_id AS SubplanId,
    sp.subplan_name AS SubplanName,
    CAST(CASE WHEN EXISTS (
        SELECT 1
        FROM msdb.dbo.sysjobschedules js
        JOIN msdb.dbo.sysschedules s ON s.schedule_id = js.schedule_id
        WHERE js.job_id = sp.job_id AND s.enabled = 1
    ) THEN 1 ELSE 0 END AS bit) AS HasSchedule,
    lr.start_time AS LastStart,
    lr.end_time AS LastEnd,
    lr.succeeded AS LastSucceeded
FROM msdb.dbo.sysmaintplan_plans p
JOIN msdb.dbo.sysmaintplan_subplans sp ON sp.plan_id = p.id
OUTER APPLY (
    SELECT TOP (1) l.start_time, l.end_time, l.succeeded
    FROM msdb.dbo.sysmaintplan_log l
    WHERE l.subplan_id = sp.subplan_id
    ORDER BY l.start_time DESC
) lr
ORDER BY p.name, sp.subplan_name;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;

        var rows = new List<SubplanRow>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var lastStart = await ReadLocalAsUtcAsync(reader, 4, ct).ConfigureAwait(false);
            var lastEnd = await ReadLocalAsUtcAsync(reader, 5, ct).ConfigureAwait(false);
            bool? succeeded = await reader.IsDBNullAsync(6, ct).ConfigureAwait(false)
                ? null
                : reader.GetBoolean(6);

            double? duration = lastStart is { } s && lastEnd is { } e && e >= s
                ? (e - s).TotalSeconds
                : null;

            rows.Add(new SubplanRow(
                PlanName: reader.GetString(0),
                SubplanId: reader.GetGuid(1),
                SubplanName: reader.GetString(2),
                HasSchedule: reader.GetBoolean(3),
                LastRunUtc: lastStart,
                DurationSeconds: duration,
                LastSucceeded: succeeded));
        }

        return rows;
    }

    // Детализация шагов ПОСЛЕДНЕГО прогона каждого под-плана из sysmaintplan_logdetail
    // (что именно делалось и чем закончилось). Привязка к последней записи sysmaintplan_log
    // по под-плану (log_id последнего start_time). line1 несёт описание шага.
    private static async Task<IReadOnlyList<DetailRow>> ReadLastRunDetailsAsync(
        SqlConnection connection, CancellationToken ct)
    {
        const string sql = @"
WITH last_log AS (
    SELECT l.task_detail_id, l.subplan_id, l.start_time,
           ROW_NUMBER() OVER (PARTITION BY l.subplan_id ORDER BY l.start_time DESC) AS rn
    FROM msdb.dbo.sysmaintplan_log l
)
SELECT
    ll.subplan_id AS SubplanId,
    d.line1 AS Detail,
    d.succeeded AS Succeeded,
    d.start_time AS StartTime
FROM last_log ll
JOIN msdb.dbo.sysmaintplan_logdetail d ON d.task_detail_id = ll.task_detail_id
WHERE ll.rn = 1
ORDER BY ll.subplan_id, d.start_time;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;

        var rows = new List<DetailRow>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var detail = await reader.IsDBNullAsync(1, ct).ConfigureAwait(false)
                ? string.Empty
                : reader.GetString(1).Trim();
            var succeeded = !await reader.IsDBNullAsync(2, ct).ConfigureAwait(false)
                && reader.GetBoolean(2);
            var start = await ReadLocalAsUtcAsync(reader, 3, ct).ConfigureAwait(false);

            rows.Add(new DetailRow(reader.GetGuid(0), detail, succeeded, start));
        }

        return rows;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Обслуживание: проба планов обслуживания недоступна (сервер {Server})")]
    private static partial void LogPlansUnavailable(ILogger logger, string server, Exception ex);

    // Сырая строка под-плана (план + под-план + расписание + последний прогон).
    private readonly record struct SubplanRow(
        string PlanName,
        Guid SubplanId,
        string SubplanName,
        bool HasSchedule,
        DateTime? LastRunUtc,
        double? DurationSeconds,
        bool? LastSucceeded);

    // Сырая строка детализации шага последнего прогона.
    private readonly record struct DetailRow(
        Guid SubplanId, string Detail, bool Succeeded, DateTime? StartUtc);
}
