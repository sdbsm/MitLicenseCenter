namespace MitLicenseCenter.Domain.Audit;

// Целочисленные значения зафиксированы — это контракт с БД (HasConversion<int>) и c
// frontend (JsonStringEnumConverter сериализует именем, но reverse-lookup по int
// допускается). Пропуски в нумерации зарезервированы под Stage 3 (см. plan).
public enum AuditActionType
{
    TenantCreated = 1,
    TenantUpdated = 2,
    TenantDeleted = 3,

    InfobaseCreated = 10,
    InfobaseUpdated = 11,
    InfobaseDeleted = 12,
    InfobaseReassigned = 13,

    // Игнор-лист «нераспределённых» баз кластера (MLC-092): скрытие служебной базы
    // из списка разбора и её возврат. Server-scope (TenantId не пишется — база ещё
    // не принадлежит клиенту). int 14/15 продолжают группу Infobase 10..13.
    UnassignedInfobaseHidden = 14,
    UnassignedInfobaseUnhidden = 15,

    PublicationCreated = 20,
    PublicationUpdated = 21,
    PublicationDeleted = 22,

    // Снятие IIS-публикации через webinst -delete (MLC-113, UX-43): приложение IIS +
    // default.vrd + web.config удалены, инфобаза в кластере не затрагивается. int 23
    // продолжает группу Publication 20..22 (frozen-int rule — число переиспользовать нельзя).
    PublicationUnpublished = 23,

    AdminLoggedIn = 100,
    AdminLoggedOut = 101,
    AdminPasswordChanged = 102,

    // Управление учётками пользователей из веб-панели (MLC-058; раздел переименован
    // «Администраторы»→«Пользователи» в MLC-060). Server-scope операции (TenantId не
    // пишется); пароль в описание НЕ кладётся — выдаётся в ответе API и показывается в
    // UI один раз. int-значения 103–106 ЗАМОРОЖЕНЫ (переименовано только имя C#).
    UserCreated = 103,
    UserDisabled = 104,
    UserPasswordReset = 105,
    UserEnabled = 106,

    // Смена роли существующей учётки Admin↔Viewer из веб-панели (MLC-061). Server-scope
    // (TenantId не пишется). Новое число — 103–106 заняты MLC-058.
    UserRoleChanged = 107,

    // Неудачная попытка входа (BE-10, MLC-114). Server-scope (TenantId не пишется).
    // initiator — введённое имя пользователя; пароль в описание НИКОГДА не кладётся.
    // Новое число — 108 (следующее свободное в 100-серии; 107 — максимум занятых,
    // frozen-int rule: число переиспользовать нельзя).
    LoginFailed = 108,

    SessionKilled = 200,
    LimitChanged = 201,

    // Publication drift (PR 3.5). Stage MLC-045 (ADR-4 переписан, ADR-4.1 revoked):
    // drift-enforcement удалён — новые строки с этими значениями НЕ пишутся. Enum-записи
    // сохранены для рендера исторических AuditLog rows (frozen-int rule).
    PublicationDriftDetected = 210,
    PublicationReconciled = 211,

    // Публикация через webinst и смена платформы (MLC-045). Published — успешная
    // (пере)публикация через webinst.exe. PlatformChanged — правка пути к wsisapi.dll
    // в web.config под новую версию платформы (default.vrd не трогается).
    PublicationPublished = 212,
    PublicationPlatformChanged = 213,

    // Управление жизненным циклом IIS (MLC-047, ADR-24). Server-scope операции из
    // веб-панели (TenantId не пишется). 227 — резерв под будущие IIS-действия.
    IisApplicationPoolRecycled = 220,
    IisApplicationPoolStarted = 221,
    IisApplicationPoolStopped = 222,
    IisSiteStarted = 223,
    IisSiteStopped = 224,
    IisSiteRestarted = 225,
    IisReset = 226,
    IisStopped = 227,
    IisStarted = 228,

    // 1С Cluster adapter circuit-breaker transitions (PR 3.2).
    // Stage 5 PR 5.1 (ADR-16): circuit breaker удалён — новые строки с этими
    // значениями НЕ пишутся. Enum-записи сохранены для рендера исторических
    // AuditLog rows (frozen-int rule, PR 2.2). Frontend i18n
    // audit.actions.ClusterAdapterCircuit{Opened,Closed} тоже сохранены.
    ClusterAdapterCircuitOpened = 300,
    ClusterAdapterCircuitClosed = 301,

    SettingChanged = 400,

    // System maintenance (PR 4.3, opens 500-серию).
    AuditLogsPurged = 500,

    // On-demand бэкап баз SQL (MLC-076, ADR-27). Requested — оператор поставил бэкап в
    // очередь; Succeeded/Failed — итог выполнения; Deleted — Admin удалил бэкап вручную;
    // Purged — ночная TTL-джоба удалила устаревшие файлы (пишется только если что-то удалено).
    BackupRequested = 510,
    BackupSucceeded = 511,
    BackupFailed = 512,
    BackupDeleted = 513,
    BackupsPurged = 514,

    // Управление службой RAS из веб-панели (MLC-159, ADR-47): новая 600-серия (frozen-int).
    // Registered — sc create новой службы; Updated — перерегистрация под платформу/порт
    // (stop→config→start); Started — запуск остановленной. Server-scope (TenantId не
    // пишется); секреты в описание не кладутся (служба слушает loopback, obj/password не
    // задаются). 603+ — резерв под будущие действия (удаление/останов вне объёма).
    RasServiceRegistered = 600,
    RasServiceUpdated = 601,
    RasServiceStarted = 602,

    // Диагностические записи раздела «Быстродействие» / Performance (MLC-179): новая
    // 700-серия (frozen-int). Started — оператор запустил расследование; Stopped — ручной
    // стоп активной записи; Deleted — удаление завершённой записи. Host-уровневые операции
    // (TenantId не пишется — запись с клиентом не связана). 703+ — резерв.
    PerfRecordingStarted = 700,
    PerfRecordingStopped = 701,
    PerfRecordingDeleted = 702,
}
