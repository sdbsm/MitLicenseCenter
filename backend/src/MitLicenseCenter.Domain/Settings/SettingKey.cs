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
    public const string IisDefaultVrdRoot = "IIS.DefaultVrdRoot";

    // Единственное место, где задан SQL-инстанс, на котором живут базы клиентов
    // (single-host, MLC-087). Форма добавления инфобазы и discovery имён БД берут
    // сервер отсюда; «дефолтом для форм» он больше не является. Без сидируемого
    // дефолта — зависит от инсталляции, оператор задаёт явно через «Параметры».
    public const string SqlServer = "Sql.Server";

    // Дефолты для формы добавления инфобазы. Не влияют на доменную модель и
    // адаптеры: значения по-прежнему хранятся per-публикация (Publication), эти
    // ключи лишь предзаполняют форму, чтобы оператор не вводил одно и то же для
    // каждой базы. Версия платформы без сидируемого дефолта — зависит от
    // конкретной инсталляции; сайт IIS почти всегда «Default Web Site».
    public const string IisDefaultSiteName = "IIS.DefaultSiteName";
    public const string OneCDefaultPlatformVersion = "OneC.DefaultPlatformVersion";

    // Polling cadence (используется в PR 3.3).
    public const string PollingHotIntervalSeconds = "Polling.HotIntervalSeconds";
    public const string PollingColdIntervalSeconds = "Polling.ColdIntervalSeconds";
    public const string PollingHotThresholdPercent = "Polling.HotThresholdPercent";

    // Drift detection cadence (используется в PR 3.5).
    public const string DriftIntervalMinutes = "Drift.IntervalMinutes";

    // Enforcement (kill) tuning. Отсрочка перед авто-завершением только что
    // подключившегося over-limit сеанса: даёт 1С проставить user-name и не бьёт
    // в окне входа. См. KillEnforcer + DECISIONS.md «Kill grace period».
    public const string EnforcementKillGraceSeconds = "Enforcement.KillGraceSeconds";

    // Audit retention (PR 4.3).
    public const string AuditRetentionDays = "Audit.RetentionDays";

    // License usage time-series retention (MLC-048, ADR-25).
    public const string LicenseUsageRetentionDays = "LicenseUsage.RetentionDays";

    // Маппинг «имя процесса → семья» для атрибуции потребления ресурсов в разделе
    // «Быстродействие» (MLC-064, ADR-26). Формат «Семья=маска,маска;…»; пусто → дефолтный
    // набор (ProcessFamilyMap.Default). Вынесен в Settings, чтобы менять без редеплоя.
    public const string PerformanceProcessFamilyMap = "Performance.ProcessFamilyMap";

    // Recording раздела «Быстродействие» (MLC-070, ADR-26, Фаза 4): период сэмплинга активной
    // записи + два независимых лимита авто-стопа (по времени и по числу сэмплов — что наступит
    // раньше). Запись ограничена собственной длительностью → ночного retention-джоба нет.
    public const string PerformanceRecordingSampleIntervalSeconds = "Performance.RecordingSampleIntervalSeconds";
    public const string PerformanceRecordingMaxDurationMinutes = "Performance.RecordingMaxDurationMinutes";
    public const string PerformanceRecordingMaxSamples = "Performance.RecordingMaxSamples";

    // On-demand бэкап баз SQL (MLC-076, ADR-27): корневая папка .bak на локальном диске
    // SQL-хоста (без default — зависит от инсталляции, как OneC.RAS.ExePath), TTL хранения,
    // потолок параллельных бэкапов на сервер и запас свободного места поверх оценки.
    public const string BackupFolderPath = "Backup.FolderPath";
    public const string BackupTtlHours = "Backup.TtlHours";
    public const string BackupMaxParallel = "Backup.MaxParallel";
    public const string BackupDiskSafetyMarginMb = "Backup.DiskSafetyMarginMb";
}
