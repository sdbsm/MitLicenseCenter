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

    // PR 3.5 — IIS XML-patch reconciliation failures (исторический код, ещё
    // используется для сбоя смены платформы — правки web.config).
    public const string IisReconcileFailed = "IIS_RECONCILE_FAILED";
    public const string IisAccessDenied = "IIS_ACCESS_DENIED";

    // MLC-045 — публикация через webinst и смена платформы.
    public const string PublishFailed = "PUBLISH_FAILED";
    public const string PublishConfirmRequired = "PUBLISH_CONFIRM_REQUIRED";

    // MLC-047 — операция управления жизненным циклом IIS (recycle/start/stop/iisreset)
    // не удалась (COM/таймаут/ненулевой exit). IisAccessDenied переиспользуется для прав.
    public const string IisOperationFailed = "IIS_OPERATION_FAILED";
    public const string IisConfirmRequired = "IIS_CONFIRM_REQUIRED";

    // MLC-002 — ручной kill сеанса.
    public const string SessionStale = "SESSION_STALE";
    public const string ClusterUnavailable = "CLUSTER_UNAVAILABLE";

    // MLC-058 — управление учётками пользователей (раздел переименован в MLC-060).
    // Строки кодов — контракт API (матчатся фронтом в matchConflictCode), меняются
    // синхронно BE↔FE.
    public const string UserUsernameDuplicate = "USER_USERNAME_DUPLICATE";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string UserCannotDisableSelf = "USER_CANNOT_DISABLE_SELF";
    public const string UserLastActiveAdmin = "USER_LAST_ACTIVE";
    // MLC-061 — смена роли существующей учётки.
    public const string UserCannotChangeOwnRole = "USER_CANNOT_CHANGE_OWN_ROLE";

    // MLC-070 — нельзя удалить идущую запись быстродействия (сначала остановить).
    public const string RecordingActive = "RECORDING_ACTIVE";
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
            "Нет доступа к IIS / файлам публикации",
            "Недостаточно прав для изменения публикации IIS или её файлов (web.config / default.vrd). "
                + "Технические подробности записаны в журнал сервера.",
            correlationId);

    // MLC-045 — публикация через webinst не удалась. detail приходит из адаптера
    // уже санитизированным (без путей/имён ИБ); сырой вывод webinst — в журнале.
    public static ProblemDetails PublishFailed(string detail, string? correlationId = null) =>
        Conflict(ProblemCodes.PublishFailed, "Публикация не удалась", detail, correlationId);

    // MLC-045 — повторная публикация перезатрёт ручную конфигурацию (Source ≠ Webinst).
    // Требуется явное подтверждение оператора (Confirm=true).
    public static ProblemDetails PublishConfirmRequired() =>
        Conflict(
            ProblemCodes.PublishConfirmRequired,
            "Требуется подтверждение",
            "Эта публикация создана не через панель (возможно, вручную в конфигураторе). "
                + "Повторная публикация через webinst перезапишет default.vrd и web.config, "
                + "удалив ручные настройки. Подтвердите, чтобы продолжить.");

    // MLC-047 — операция управления IIS не удалась. Наружу — санитизированный русский
    // текст без COM/путей; технические детали в журнале по correlationId.
    public static ProblemDetails IisOperationFailed(string? correlationId = null) =>
        Conflict(
            ProblemCodes.IisOperationFailed,
            "Операция IIS не удалась",
            "Не удалось выполнить операцию управления IIS. Технические подробности записаны в журнал сервера.",
            correlationId);

    // MLC-047 — разрушительная операция IIS (recycle пула / iisreset / остановка)
    // требует явного подтверждения оператора (Confirm=true). Защита от случайного клика
    // помимо токена-подтверждения в UI.
    public static ProblemDetails IisConfirmRequired() =>
        Conflict(
            ProblemCodes.IisConfirmRequired,
            "Требуется подтверждение",
            "Эта операция перезапустит IIS или пул приложений и временно прервёт работу "
                + "опубликованных баз. Подтвердите, чтобы продолжить.");

    // MLC-002 — снапшот устарел: сеанс с тем же SessionId сменил дескриптор
    // (InfobaseId/AppID/StartedAt). 409 — оператору нужно обновить список.
    public static ProblemDetails SessionStale() =>
        Conflict(
            ProblemCodes.SessionStale,
            "Сеанс изменился",
            "Сеанс изменился с момента обновления списка (возможно, перезапущен). Обновите список сеансов и повторите.");

    // ── Управление учётками пользователей (MLC-058; раздел переименован в MLC-060) ──
    public static ProblemDetails UserUsernameDuplicate(string userName) =>
        Conflict(
            ProblemCodes.UserUsernameDuplicate,
            "Дубликат логина",
            $"Учётная запись с логином «{userName}» уже существует.");

    public static ProblemDetails UserCannotDisableSelf() =>
        Conflict(
            ProblemCodes.UserCannotDisableSelf,
            "Нельзя отключить себя",
            "Нельзя отключить собственную учётную запись.");

    // Считаются именно учётки роли Admin (не любой пользователь) — иначе панелью некому
    // будет управлять; текст про «администратора» относится к роли, не к разделу. Общий
    // для отключения (MLC-058) и разжалования роли (MLC-061) — фронт подбирает точную
    // формулировку по коду в своём контексте.
    public static ProblemDetails UserLastActiveAdmin() =>
        Conflict(
            ProblemCodes.UserLastActiveAdmin,
            "Последний активный администратор",
            "Нельзя оставить панель без активного администратора — это последний активный администратор.");

    // MLC-061 — нельзя менять роль собственной учётке: само-разжалование = потеря доступа.
    public static ProblemDetails UserCannotChangeOwnRole() =>
        Conflict(
            ProblemCodes.UserCannotChangeOwnRole,
            "Нельзя сменить роль себе",
            "Нельзя менять роль собственной учётной записи.");

    // MLC-070 — удаление идущей записи быстродействия запрещено: сначала остановить, потом удалять
    // (иначе фоновый сэмплер продолжит писать в удалённую запись).
    public static ProblemDetails RecordingActive() =>
        Conflict(
            ProblemCodes.RecordingActive,
            "Запись идёт",
            "Эта запись быстродействия ещё идёт. Сначала остановите её, затем удалите.");

    // 404 для несуществующей учётки. Не 409, поэтому отдельный helper со Status=404 и
    // machine-readable code (frontend сопоставляет код, как и с конфликтами).
    public static ProblemDetails UserNotFound()
    {
        var problem = new ProblemDetails
        {
            Type = "https://mitlicense.center/problems/not-found",
            Title = "Учётная запись не найдена",
            Status = StatusCodes.Status404NotFound,
            Detail = "Учётная запись не найдена (возможно, удалена). Обновите список.",
        };
        problem.Extensions["code"] = ProblemCodes.UserNotFound;
        return problem;
    }

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
