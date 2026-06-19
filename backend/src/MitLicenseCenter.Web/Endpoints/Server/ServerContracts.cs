using System.ComponentModel.DataAnnotations;

namespace MitLicenseCenter.Web.Endpoints;

// Контракты раздела «Сервер» (MLC-213, ADR-54/55). camelCase на проводе; nullable-поля
// опускаются при null (гоча api-omits-null-fields) — FE-схемы (MLC-214) это учитывают.
// enum'ы (health/state) — строкой, как везде.

// Сводный статус узла: список серверов 1С + сводки RAS/SQL/IIS + общий индикатор здоровья.
public sealed record ServerStatusResponse(
    IReadOnlyList<OneCServerDto> OneCServers,
    RasStatusDto Ras,
    SqlStatusDto Sql,
    IisStatusDto Iis,
    string Overall);

// Одна служба сервера 1С (ragent). platformVersion — best-effort из ImagePath.
public sealed record OneCServerDto(string ServiceName, bool Running, string? PlatformVersion);

// Сводка RAS (только наблюдение — управление в /ras-service/*). available:false + error —
// диагностику снять не удалось.
public sealed record RasStatusDto(
    string State,
    bool Running,
    string? ServiceName,
    bool Available,
    string? Error);

// Сводка локальной службы SQL (только наблюдение — управление SQL вне объёма, ADR-54).
public sealed record SqlStatusDto(
    string? Instance,
    string? ServiceName,
    bool Running,
    bool Available,
    string? Error);

// Сводка IIS (только наблюдение — управление в /api/v1/iis/*).
public sealed record IisStatusDto(string State, bool Available, string? Error);

// Запрос старта сервера 1С: имя службы ragent (whitelist по discovery). Confirm не нужен
// (запуск не разрушителен).
public sealed record OneCServerStartRequest(
    [property: Required] string ServiceName);

// Запрос остановки/перезапуска сервера 1С: имя службы + серверный Confirm-гейт
// (разрушительная операция — прерывает работу всех баз узла).
public sealed record OneCServerStopRequest(
    [property: Required] string ServiceName,
    bool Confirm = false);

// Ответ мутации: имя службы + итоговое верифицированное состояние службы
// ("Running"/"Stopped" — WindowsServiceStatus).
public sealed record ServerOperationResponse(string ServiceName, string FinalStatus);

// Свежесть резервных копий баз — вкладка «Обслуживание» (MLC-216, ADR-54). status строкой
// ("Ok"/"PermissionDenied"/"Unavailable" — MaintenanceProbeStatus): деградация (нет прав на
// msdb.dbo.backupset / SQL недоступен) — статусом, а не 500; в degraded-ветке databases пуст.
public sealed record BackupFreshnessResponse(
    string Status,
    IReadOnlyList<DatabaseBackupFreshnessDto> Databases);

// Свежесть последнего бэкапа одной базы. lastFull/lastDiff/lastLog — время завершения
// последнего бэкапа типа (UTC, ISO-8601), null опускается (гоча api-omits-null-fields).
// isStale — вычисленный флаг «устарел» (нет FULL или последний FULL старше ~26ч, BackupFreshnessPolicy).
public sealed record DatabaseBackupFreshnessDto(
    string DatabaseName,
    DateTime? LastFullUtc,
    DateTime? LastDiffUtc,
    DateTime? LastLogUtc,
    bool IsStale);

// Планы обслуживания SQL — вкладка «Обслуживание» (MLC-217, ADR-54). status строкой
// ("Ok"/"AgentUnavailable"/"PermissionDenied"/"Unavailable" — MaintenancePlansStatus):
// деградация (нет прав на историю планов / SQL Agent недоступен / SQL недоступен) — статусом,
// а не 500; в degraded-ветке plans пуст.
public sealed record MaintenancePlansResponse(
    string Status,
    IReadOnlyList<MaintenancePlanDto> Plans);

// План обслуживания с под-планами.
public sealed record MaintenancePlanDto(
    string Name,
    IReadOnlyList<MaintenanceSubplanDto> Subplans);

// Под-план: имя, флаг «по расписанию», итог последнего прогона строкой
// ("Succeeded"/"Failed"/"Overdue"/"NeverRun"/"Unknown" — MaintenanceRunOutcome),
// время последнего прогона (UTC, ISO-8601) и длительность в секундах (null опускаются),
// детализация по шагам последнего прогона.
public sealed record MaintenanceSubplanDto(
    string Name,
    bool HasSchedule,
    string Outcome,
    DateTime? LastRunUtc,
    double? DurationSeconds,
    IReadOnlyList<MaintenanceTaskDetailDto> Tasks);

// Один шаг последнего прогона под-плана: описание (что делалось) + успех.
public sealed record MaintenanceTaskDetailDto(string Detail, bool Succeeded);

// Расписание авто-рестартов сервера 1С — карточка «Расписание авто-рестартов» во вкладке
// «Службы» (MLC-218, ADR-55). enabled — включён ли ночной авто-рестарт; time — время
// суток HH:mm по часам хоста; lastRunUtc — время последнего прогона джобы (UTC, ISO-8601,
// null опускается — ещё не запускалась); targetServices — текущие ЗАПУЩЕННЫЕ службы ragent
// (что именно рестартнётся; пусто = сервер 1С не запущен/не найден).
public sealed record AutoRestartScheduleResponse(
    bool Enabled,
    string Time,
    DateTime? LastRunUtc,
    IReadOnlyList<string> TargetServices);

// Запрос изменения расписания (Admin): вкл/выкл + время HH:mm (валидируется на BE).
public sealed record AutoRestartScheduleRequest(
    bool Enabled,
    string Time);

// Рабочие процессы 1С (rphost) — блок во вкладке «Службы» (MLC-219, ADR-54). Снимок
// `rac process list` через тот же live-pull, что и «Быстродействие». Пустой список =
// rac не настроен/недоступен (best-effort, never-throws — эндпоинт не 500-ит).
public sealed record OneCProcessesResponse(
    IReadOnlyList<OneCProcessDto> Processes);

// Один рабочий процесс кластера. process — UUID рабочего процесса (rphost); pid —
// ОС-идентификатор; availablePerformance — APDEX-подобный индикатор (↓ = деградация,
// при capacity 1000); avgCallTime — средняя длительность вызова в секундах (дробная);
// memorySize — размер занятой памяти в байтах. Nullable-поля опускаются при null
// (гоча api-omits-null-fields) — парсер «never throws», на иных версиях платформы их
// может не быть.
public sealed record OneCProcessDto(
    Guid Process,
    int? Pid,
    int? AvailablePerformance,
    double? AvgCallTime,
    long? MemorySize);
