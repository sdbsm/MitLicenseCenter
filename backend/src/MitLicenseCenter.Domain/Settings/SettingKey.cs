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

    // IIS / publications (используется в PR 3.5).
    public const string IisServiceAccountUserName = "IIS.ServiceAccount.UserName";
    public const string IisDefaultVrdRoot = "IIS.DefaultVrdRoot";

    // Polling cadence (используется в PR 3.3).
    public const string PollingHotIntervalSeconds = "Polling.HotIntervalSeconds";
    public const string PollingColdIntervalSeconds = "Polling.ColdIntervalSeconds";
    public const string PollingHotThresholdPercent = "Polling.HotThresholdPercent";

    // Drift detection cadence (используется в PR 3.5).
    public const string DriftIntervalMinutes = "Drift.IntervalMinutes";

    // Audit retention (PR 4.3).
    public const string AuditRetentionDays = "Audit.RetentionDays";
}
