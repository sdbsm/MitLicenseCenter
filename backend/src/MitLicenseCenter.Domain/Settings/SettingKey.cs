namespace MitLicenseCenter.Domain.Settings;

// Whitelist runtime-параметров системы. Источник правды — этот класс плюс
// SettingDefinitions catalog в Application/Settings: всё, чего нет в catalog,
// получает 404 SETTING_UNKNOWN_KEY на PUT. Имена ключей — wire-контракт
// (frontend и audit description оперируют ровно этими строками), не менять
// после релиза без миграции существующих row'ов.
public static class SettingKey
{
    // 1С Cluster REST API.
    public const string OneCClusterRestApiUrl = "OneC.Cluster.RestApiUrl";
    public const string OneCClusterAdminUser = "OneC.Cluster.AdminUser";
    public const string OneCClusterAdminPassword = "OneC.Cluster.AdminPassword";
    public const string OneCClusterRestApiTimeoutSeconds = "OneC.Cluster.RestApiTimeoutSeconds";

    // RAS fallback (используется в PR 3.8, объявлен заранее).
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

    // Circuit breaker (используется в PR 3.2).
    public const string CircuitBreakerProbeIntervalSeconds = "CircuitBreaker.ProbeIntervalSeconds";
    public const string CircuitBreakerFailureCount = "CircuitBreaker.FailureCount";

    // Audit retention (PR 4.3).
    public const string AuditRetentionDays = "Audit.RetentionDays";
}
