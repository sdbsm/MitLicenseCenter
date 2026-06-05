namespace MitLicenseCenter.Domain.Settings;

// Whitelist runtime-параметров системы. Источник правды — этот класс плюс
// SettingDefinitions catalog в Application/Settings: всё, чего нет в catalog,
// получает 404 SETTING_UNKNOWN_KEY на PUT. Имена ключей — wire-контракт
// (frontend и audit description оперируют ровно этими строками), не менять
// после релиза без миграции существующих row'ов.
public static class SettingKey
{
    // 1С Cluster admin credentials. После Stage 5 PR 5.1 (ADR-16) REST adapter
    // удалён — эти ключи остаются как auth-источник для RAS rac.exe адаптера
    // (флаги --cluster-user / --cluster-pwd, см. ADR-3.3). Если кластер
    // не имеет зарегистрированных администраторов, оба поля можно оставить
    // пустыми — rac.exe выполняется анонимно.
    public const string OneCClusterAdminUser = "OneC.Cluster.AdminUser";
    public const string OneCClusterAdminPassword = "OneC.Cluster.AdminPassword";

    // RAS adapter (PR 3.8 + Stage 5 PR 5.1: единственный 1С cluster-адаптер).
    public const string OneCRasEndpoint = "OneC.RAS.Endpoint";
    public const string OneCRasExePath = "OneC.RAS.ExePath";

    // Whitelist client-типов 1С (app-id), потребляющих лицензию (MLC-024). Список через
    // запятую; пусто → дефолтный набор (LicenseConsumingAppIds.Default). Вынесен в
    // Settings, чтобы менять политику без редеплоя.
    public const string OneCLicenseConsumingAppIds = "OneC.LicenseConsumingAppIds";

    // IIS / publications (используется в PR 3.5).
    public const string IisDefaultVrdRoot = "IIS.DefaultVrdRoot";

    // Адрес 1С-кластера для строки соединения webinst (MLC-045): -connstr
    // "Srvr=<этот адрес>;Ref=<имя ИБ>;". Формат host или host:port. Пусто →
    // берём host из OneC.RAS.Endpoint (кластер и RAS обычно на одном сервере).
    public const string OneCClusterServer = "OneC.Cluster.Server";

    // Дефолты для формы добавления инфобазы. Не влияют на доменную модель и
    // адаптеры: значения по-прежнему хранятся per-база (Infobase/Publication),
    // эти ключи лишь предзаполняют форму, чтобы оператор не вводил одно и то же
    // для каждой базы. Сервер БД и версия платформы без сидируемого дефолта —
    // зависят от конкретной инсталляции; сайт IIS почти всегда «Default Web Site».
    public const string DefaultsDatabaseServer = "Defaults.DatabaseServer";
    public const string IisDefaultSiteName = "IIS.DefaultSiteName";
    public const string OneCDefaultPlatformVersion = "OneC.DefaultPlatformVersion";

    // Polling cadence (используется в PR 3.3).
    public const string PollingHotIntervalSeconds = "Polling.HotIntervalSeconds";
    public const string PollingColdIntervalSeconds = "Polling.ColdIntervalSeconds";
    public const string PollingHotThresholdPercent = "Polling.HotThresholdPercent";

    // Drift detection cadence (используется в PR 3.5).
    public const string DriftIntervalMinutes = "Drift.IntervalMinutes";

    // Audit retention (PR 4.3).
    public const string AuditRetentionDays = "Audit.RetentionDays";
}
