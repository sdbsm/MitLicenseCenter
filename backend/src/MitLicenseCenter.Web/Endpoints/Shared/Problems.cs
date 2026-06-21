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

    // MLC-113 — снятие IIS-публикации через webinst -delete не удалось.
    public const string UnpublishFailed = "UNPUBLISH_FAILED";

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

    // MLC-239 — операции над «Делом» расследования:
    //  • InvestigationActive — нельзя удалить активное дело (сначала остановить);
    //  • InvestigationNotActive — стоп переданного дела невозможен (оно не текущее активное);
    //  • InvestigationStartFailed — старт сбора не удался (нет прав/места/корень 1С/уже идёт) —
    //    detail несёт причину, а где применимо — точную команду icacls для оператора.
    public const string InvestigationActive = "INVESTIGATION_ACTIVE";
    public const string InvestigationNotActive = "INVESTIGATION_NOT_ACTIVE";
    public const string InvestigationStartFailed = "INVESTIGATION_START_FAILED";

    // MLC-077 — бэкапы баз SQL (ADR-27): дубль активной базы / незаданная папка /
    // провал server-side удаления файла.
    public const string BackupActive = "BACKUP_ACTIVE";
    public const string BackupFolderNotConfigured = "BACKUP_FOLDER_NOT_CONFIGURED";
    public const string BackupDeleteFailed = "BACKUP_DELETE_FAILED";

    // MLC-088 (single-host) — SQL-инстанс (настройка Sql.Server) не задан: бэкап брать
    // не с чего (сервер БД больше не хранится per-база).
    public const string SqlServerNotConfigured = "SQL_SERVER_NOT_CONFIGURED";

    // MLC-092 — игнор-лист «нераспределённых» баз кластера: нельзя скрыть базу,
    // уже заведённую в панель / уже скрытую.
    public const string UnassignedAlreadyAssigned = "UNASSIGNED_ALREADY_ASSIGNED";
    public const string UnassignedAlreadyHidden = "UNASSIGNED_ALREADY_HIDDEN";

    // MLC-136 (R12c) — оптимистическая блокировка клиента: апдейт с устаревшим
    // rowversion-токеном (данные изменены другим пользователем между чтением формы и
    // сохранением). Фронт по этому коду показывает тост «обновите страницу и повторите».
    public const string TenantConcurrencyConflict = "TENANT_CONCURRENCY_CONFLICT";

    // MLC-151 — оптимистическая блокировка инфобазы и публикации (зеркаль MLC-136).
    // Апдейт с устаревшим rowversion-токеном (данные изменены другим пользователем между
    // чтением формы и сохранением). Фронт по коду показывает тост «обновите и повторите».
    // Инфобаза — корень aggregate'а (PUT /infobases/{id} правит и инфобазу, и публикацию);
    // публикация — собственный код, т.к. у неё есть самостоятельный PUT /publications/{id}.
    public const string InfobaseConcurrencyConflict = "INFOBASE_CONCURRENCY_CONFLICT";
    public const string PublicationConcurrencyConflict = "PUBLICATION_CONCURRENCY_CONFLICT";

    // MLC-159 (ADR-47) — операция над службой RAS (register/update/start) не удалась:
    // неготовое окружение (нет ras.exe выбранной платформы / не задан порт-версия /
    // служба не найдена) либо ненулевой код sc.exe. Фронт по коду показывает текст из detail.
    public const string RasServiceOperationFailed = "RAS_SERVICE_OPERATION_FAILED";

    // MLC-213 (ADR-54/55) — мутация сервера 1С (start/stop/restart) не достигла целевого
    // состояния (жёсткий сбой sc / служба не найдена / истёк таймаут верификации). Фронт по
    // коду показывает текст из detail. ServerConfirmRequired — серверный Confirm-гейт на
    // разрушительные stop/restart (защита от случайного клика помимо токена в UI).
    public const string ServerOperationFailed = "SERVER_OPERATION_FAILED";
    public const string ServerConfirmRequired = "SERVER_CONFIRM_REQUIRED";

    // MLC-220 (ADR-56) — рестарт рабочего процесса 1С (rphost) по Pid не выполнен.
    // ProcessConfirmRequired — серверный Confirm-гейт (разрушительно: рвёт активные сеансы
    // на процессе). ProcessRestartFailed — guard/верификация не пройдены: Pid переиспользован
    // ОС (не rphost) либо процесс не исчез из rac process list за таймаут. Фронт по коду
    // показывает текст из detail.
    public const string ProcessConfirmRequired = "PROCESS_CONFIRM_REQUIRED";
    public const string ProcessRestartFailed = "PROCESS_RESTART_FAILED";
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

    // MLC-113 — снятие публикации через webinst -delete не удалось. detail приходит из
    // адаптера санитизированным; сырой вывод webinst — в журнале. При снятии в составе
    // удаления инфобазы это 409 без удаления строк (защита от молчаливого сиротства IIS).
    public static ProblemDetails UnpublishFailed(string detail, string? correlationId = null) =>
        Conflict(ProblemCodes.UnpublishFailed, "Снятие публикации не удалось", detail, correlationId);

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

    // MLC-239 — нельзя удалить активное «Дело» расследования (идёт сбор ТЖ): сначала остановить,
    // затем удалять. Зеркаль RecordingActive.
    public static ProblemDetails InvestigationActive() =>
        Conflict(
            ProblemCodes.InvestigationActive,
            "Дело активно",
            "Это расследование ещё идёт (собирается технологический журнал). Сначала остановите его, затем удалите.");

    // MLC-239 — стоп переданного дела невозможен: оно не является текущим активным сбором (уже снято/
    // завершено/не существует). 409 — оператору обновить список.
    public static ProblemDetails InvestigationNotActive() =>
        Conflict(
            ProblemCodes.InvestigationNotActive,
            "Дело не активно",
            "Это расследование не является текущим активным сбором (возможно, уже остановлено или завершено). Обновите список.");

    // MLC-239 — старт сбора ТЖ не удался (исход TechLogStartOutcome ≠ Started). detail санитизирован
    // (русский текст причины из сервиса: нет прав на logcfg.xml / корень 1С не найден / мало места /
    // нет прав агента — где применимо, с точной командой icacls). Уже-идёт обрабатывается отдельно
    // (AlreadyActive → 409 InvestigationActive с id текущего дела).
    public static ProblemDetails InvestigationStartFailed(string detail) =>
        Conflict(ProblemCodes.InvestigationStartFailed, "Не удалось запустить расследование", detail);

    // MLC-077 — у этой базы уже есть бэкап в очереди или выполняется (per-db замок, ADR-27).
    // Общий и для POST-дубля, и для DELETE идущего бэкапа — фронт подбирает формулировку
    // по контексту (образец UserLastActiveAdmin).
    public static ProblemDetails BackupActive() =>
        Conflict(
            ProblemCodes.BackupActive,
            "Бэкап уже выполняется",
            "Бэкап этой базы уже стоит в очереди или выполняется. Дождитесь его завершения.");

    // MLC-077 — корневая папка бэкапов не задана: честное «бэкап не настроен» вместо
    // постановки заведомо провального запроса в очередь.
    public static ProblemDetails BackupFolderNotConfigured() =>
        Conflict(
            ProblemCodes.BackupFolderNotConfigured,
            "Папка бэкапов не настроена",
            "Корневая папка для бэкапов не задана. Укажите настройку Backup.FolderPath в разделе «Параметры».");

    // MLC-088 (single-host) — SQL-инстанс не задан: сервер БД больше не хранится per-база,
    // бэкап берёт его из настройки Sql.Server. Честное «не настроено» вместо провала адаптера.
    public static ProblemDetails SqlServerNotConfigured() =>
        Conflict(
            ProblemCodes.SqlServerNotConfigured,
            "SQL-сервер не настроен",
            "SQL-инстанс не задан. Укажите настройку «SQL Server» в разделе «Параметры».");

    // MLC-077 — server-side удаление .bak не удалось: запись о бэкапе сохранена, чтобы файл
    // не осиротел невидимым. Технические детали — в журнале сервера (адаптер never-throws).
    public static ProblemDetails BackupDeleteFailed() =>
        Conflict(
            ProblemCodes.BackupDeleteFailed,
            "Не удалось удалить файл бэкапа",
            "Файл бэкапа удалить не удалось — запись сохранена. Технические подробности записаны в журнал сервера.");

    // MLC-092 — скрывать можно только «нераспределённую» базу: заведённая в панель
    // в списке разбора не появляется, hide для неё — устаревшее состояние клиента.
    public static ProblemDetails UnassignedAlreadyAssigned() =>
        Conflict(
            ProblemCodes.UnassignedAlreadyAssigned,
            "База уже заведена",
            "Эта база кластера уже заведена в панель — скрывать её не нужно. Обновите список.");

    // MLC-092 — повторный hide: запись игнор-листа уже существует.
    public static ProblemDetails UnassignedAlreadyHidden() =>
        Conflict(
            ProblemCodes.UnassignedAlreadyHidden,
            "База уже скрыта",
            "Эта база кластера уже скрыта. Обновите список.");

    // MLC-136 (R12c) — оптимистическая блокировка: rowversion клиента, прочитанный при
    // загрузке формы, устарел (клиент изменён другим пользователем). 409 — оператору
    // нужно перечитать актуальные данные и повторить.
    public static ProblemDetails TenantConcurrencyConflict() =>
        Conflict(
            ProblemCodes.TenantConcurrencyConflict,
            "Данные клиента изменены",
            "Данные клиента изменены другим пользователем. Обновите страницу и повторите.");

    // MLC-151 — оптимистическая блокировка инфобазы: rowversion инфобазы, прочитанный при
    // загрузке формы, устарел (запись изменена другим пользователем). 409 — оператору
    // нужно перечитать актуальные данные и повторить.
    public static ProblemDetails InfobaseConcurrencyConflict() =>
        Conflict(
            ProblemCodes.InfobaseConcurrencyConflict,
            "Данные инфобазы изменены",
            "Данные инфобазы изменены другим пользователем. Обновите страницу и повторите.");

    // MLC-151 — оптимистическая блокировка публикации (самостоятельный PUT /publications/{id}):
    // rowversion публикации, прочитанный при загрузке, устарел.
    public static ProblemDetails PublicationConcurrencyConflict() =>
        Conflict(
            ProblemCodes.PublicationConcurrencyConflict,
            "Данные публикации изменены",
            "Данные публикации изменены другим пользователем. Обновите страницу и повторите.");

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

    // MLC-159 (ADR-47) — операция над службой RAS не удалась. detail приходит из адаптера
    // (RasServiceOperationException) уже санитизированным русским текстом: причина
    // неготовности окружения или ненулевой код sc.exe. Секреты в detail отсутствуют
    // (служба слушает loopback, obj/password не задаются). Технические подробности — в
    // журнале сервера по correlationId.
    public static ProblemDetails RasServiceOperationFailed(string detail, string? correlationId = null) =>
        Conflict(ProblemCodes.RasServiceOperationFailed, "Операция со службой RAS не удалась", detail, correlationId);

    // MLC-213 (ADR-54/55) — мутация сервера 1С не удалась. detail приходит из доменного
    // WindowsServiceOperationException уже санитизированным русским текстом (жёсткий сбой sc /
    // служба не найдена / истёк таймаут верификации). Секретов в detail нет. Технические
    // подробности — в журнале сервера по correlationId.
    public static ProblemDetails ServerOperationFailed(string detail, string? correlationId = null) =>
        Conflict(ProblemCodes.ServerOperationFailed, "Операция с сервером 1С не удалась", detail, correlationId);

    // MLC-213 — разрушительная операция над сервером 1С (остановка/перезапуск) требует
    // явного подтверждения оператора (Confirm=true). Защита от случайного клика помимо
    // токена-подтверждения в UI (образец IisConfirmRequired).
    public static ProblemDetails ServerConfirmRequired() =>
        Conflict(
            ProblemCodes.ServerConfirmRequired,
            "Требуется подтверждение",
            "Эта операция остановит или перезапустит сервер 1С и временно прервёт работу всех баз узла. "
                + "Подтвердите, чтобы продолжить.");

    // MLC-220 (ADR-56) — рестарт rphost требует подтверждения (разрушительно: рвёт активные
    // сеансы на этом рабочем процессе). Защита от случайного клика помимо токена в UI.
    public static ProblemDetails ProcessConfirmRequired() =>
        Conflict(
            ProblemCodes.ProcessConfirmRequired,
            "Требуется подтверждение",
            "Перезапуск рабочего процесса 1С разорвёт активные сеансы на этом процессе. "
                + "Подтвердите, чтобы продолжить.");

    // MLC-220 (ADR-56) — рестарт rphost не выполнен: guard/верификация не пройдены. detail —
    // санитизированный русский текст (Pid переиспользован ОС / процесс не исчез за таймаут).
    // Секретов нет. Технические подробности — в журнале сервера по correlationId.
    public static ProblemDetails ProcessRestartFailed(string detail, string? correlationId = null) =>
        Conflict(ProblemCodes.ProcessRestartFailed, "Перезапуск рабочего процесса 1С не удался", detail, correlationId);

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
