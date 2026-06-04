using Microsoft.AspNetCore.Mvc;

namespace MitLicenseCenter.Web.Endpoints;

// Заводим machine-readable code'ы для 409-конфликтов отдельной константой —
// frontend ориентируется на них, чтобы переводить ошибку в локализованное
// сообщение и помечать конкретное поле формы.
public static class ProblemCodes
{
    public const string NameDuplicate = "NAME_DUPLICATE";
    public const string TenantHasInfobases = "TENANT_HAS_INFOBASES";
    public const string NameDuplicateInTenant = "NAME_DUPLICATE_IN_TENANT";
    public const string InfobaseAlreadyAssigned = "INFOBASE_ALREADY_ASSIGNED";
    public const string InfobaseNameTakenInTarget = "INFOBASE_NAME_TAKEN_IN_TARGET";

    public const string SettingUnknownKey = "SETTING_UNKNOWN_KEY";
    public const string SettingInvalidValue = "SETTING_INVALID_VALUE";

    // PR 3.5 — IIS XML-patch reconciliation failures.
    public const string IisReconcileFailed = "IIS_RECONCILE_FAILED";
    public const string IisAccessDenied = "IIS_ACCESS_DENIED";

    // MLC-002 — ручной kill сеанса.
    public const string SessionStale = "SESSION_STALE";
    public const string ClusterUnavailable = "CLUSTER_UNAVAILABLE";
}

public static class Problems
{
    public static ProblemDetails Conflict(string code, string title, string detail, string? correlationId = null)
    {
        var problem = new ProblemDetails
        {
            Type = "https://mitlicense.center/problems/conflict",
            Title = title,
            Status = StatusCodes.Status409Conflict,
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        // MLC-009: машинно-читаемый идентификатор для сопоставления с записью в
        // журнале сервера. Сам текст исключения наружу не уходит — оператор
        // находит детали в логе по correlationId.
        if (!string.IsNullOrEmpty(correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        return problem;
    }

    public static ProblemDetails TenantNameDuplicate(string name) =>
        Conflict(
            ProblemCodes.NameDuplicate,
            "Дубликат названия клиента",
            $"Клиент с названием «{name}» уже существует.");

    public static ProblemDetails TenantHasInfobases() =>
        Conflict(
            ProblemCodes.TenantHasInfobases,
            "Удаление невозможно",
            "У клиента есть инфобазы — сначала удалите их.");

    public static ProblemDetails InfobaseNameDuplicateInTenant(string name) =>
        Conflict(
            ProblemCodes.NameDuplicateInTenant,
            "Дубликат названия инфобазы",
            $"Инфобаза с названием «{name}» уже существует у этого клиента.");

    public static ProblemDetails InfobaseAlreadyAssigned() =>
        Conflict(
            ProblemCodes.InfobaseAlreadyAssigned,
            "База уже привязана",
            "Эта база кластера уже привязана к другому клиенту.");

    public static ProblemDetails InfobaseNameTakenInTarget(string name) =>
        Conflict(
            ProblemCodes.InfobaseNameTakenInTarget,
            "Конфликт названия при переносе",
            $"У целевого клиента уже есть инфобаза с названием «{name}». Переименуйте базу перед переносом.");

    // MLC-009: наружу — только санитизированный русский текст без имён серверов,
    // путей и текста COM/IO-исключения. Технические детали логируются и находятся
    // по необязательному correlationId.
    public static ProblemDetails IisReconcileFailed(string? correlationId = null) =>
        Conflict(
            ProblemCodes.IisReconcileFailed,
            "Согласование не удалось",
            "Не удалось согласовать публикацию IIS. Технические подробности записаны в журнал сервера.",
            correlationId);

    public static ProblemDetails IisAccessDenied(string? correlationId = null) =>
        Conflict(
            ProblemCodes.IisAccessDenied,
            "Нет доступа к IIS / default.vrd",
            "Недостаточно прав для изменения публикации IIS или файла default.vrd. "
                + "Технические подробности записаны в журнал сервера.",
            correlationId);

    // MLC-002 — снапшот устарел: сеанс с тем же SessionId сменил дескриптор
    // (InfobaseId/AppID/StartedAt). 409 — оператору нужно обновить список.
    public static ProblemDetails SessionStale() =>
        Conflict(
            ProblemCodes.SessionStale,
            "Сеанс изменился",
            "Сеанс изменился с момента обновления списка (возможно, перезапущен). Обновите список сеансов и повторите.");

    // MLC-002 — kill не выполнен: кластер 1С (RAS) недоступен или вернул ошибку.
    // 502 (upstream-сбой). Запись в аудит при этом НЕ делается.
    public static ProblemDetails ClusterUnavailable()
    {
        var problem = new ProblemDetails
        {
            Type = "https://mitlicense.center/problems/cluster-unavailable",
            Title = "Кластер 1С недоступен",
            Status = StatusCodes.Status502BadGateway,
            Detail = "Не удалось завершить сеанс: кластер 1С (RAS) недоступен или вернул ошибку. "
                + "Сеанс не завершён, запись в аудит не сделана. Повторите попытку позже.",
        };
        problem.Extensions["code"] = ProblemCodes.ClusterUnavailable;
        return problem;
    }
}
