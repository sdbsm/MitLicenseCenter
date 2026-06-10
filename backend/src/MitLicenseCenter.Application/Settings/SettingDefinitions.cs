using MitLicenseCenter.Application.Performance;
using MitLicenseCenter.Domain.Settings;

namespace MitLicenseCenter.Application.Settings;

public enum SettingValueKind
{
    Text,
    Number,
    Url,
    HostPort,
    Path,
}

// Полный whitelist параметров + валидаторы + дефолты. Endpoint валидирует
// против этого dictionary, seeder из него же сидит дефолты при первом запуске.
public sealed record SettingDefinition(
    string Key,
    bool IsSecret,
    string Description,
    SettingValueKind Kind,
    string? DefaultValue = null,
    int? Min = null,
    int? Max = null);

public static class SettingDefinitions
{
    public static readonly IReadOnlyDictionary<string, SettingDefinition> All =
        new Dictionary<string, SettingDefinition>(StringComparer.Ordinal)
        {
            [SettingKey.OneCClusterAdminUser] = new(
                SettingKey.OneCClusterAdminUser,
                IsSecret: false,
                Description: "Логин администратора кластера 1С (используется rac.exe RAS-адаптером — флаг --cluster-user). Оставьте пустым для анонимного доступа.",
                Kind: SettingValueKind.Text),

            [SettingKey.OneCClusterAdminPassword] = new(
                SettingKey.OneCClusterAdminPassword,
                IsSecret: true,
                Description: "Пароль администратора кластера 1С (используется rac.exe — флаг --cluster-pwd). Хранится зашифрованным DPAPI.",
                Kind: SettingValueKind.Text),

            [SettingKey.OneCRasEndpoint] = new(
                SettingKey.OneCRasEndpoint,
                IsSecret: false,
                Description: "Endpoint RAS-сервера в формате host:port (по умолчанию localhost:1545).",
                Kind: SettingValueKind.HostPort),

            // ADR-3.3: дефолт не сидируем — 1С 8.5 положил rac.exe в версионную
            // папку (`1cv8\<version>\bin\`), в `1cv8\common\` его нет. Оператор
            // обязан задать явно через «Параметры» при настройке RAS fallback'а.
            [SettingKey.OneCRasExePath] = new(
                SettingKey.OneCRasExePath,
                IsSecret: false,
                Description: "Путь к rac.exe для RAS fallback (например, C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\rac.exe).",
                Kind: SettingValueKind.Path),

            [SettingKey.OneCLicenseConsumingAppIds] = new(
                SettingKey.OneCLicenseConsumingAppIds,
                IsSecret: false,
                Description: "Список client-типов 1С (app-id), потребляющих серверную лицензию, через запятую. Регистр не важен. Пусто → стандартный набор (1CV8, 1CV8C, WebClient, Designer, COMConnection).",
                Kind: SettingValueKind.Text,
                DefaultValue: LicenseConsumingAppIds.Default),

            [SettingKey.IisDefaultVrdRoot] = new(
                SettingKey.IisDefaultVrdRoot,
                IsSecret: false,
                Description: "Базовый каталог публикаций 1С на IIS. Физический путь новой публикации форма предлагает как {этот корень}\\{имя базы}.",
                Kind: SettingValueKind.Path,
                DefaultValue: @"C:\inetpub\wwwroot"),

            // Единственное место, где задан SQL-инстанс, на котором живут базы клиентов
            // (single-host, MLC-087): discovery имён БД и форма инфобазы берут сервер
            // отсюда. На бекенде читается discovery'ем и постановкой бэкапа в очередь.
            [SettingKey.SqlServer] = new(
                SettingKey.SqlServer,
                IsSecret: false,
                Description: "SQL-инстанс, на котором живут базы клиентов (например, sql.local или (local)).",
                Kind: SettingValueKind.Text),

            [SettingKey.IisDefaultSiteName] = new(
                SettingKey.IisDefaultSiteName,
                IsSecret: false,
                Description: "Сайт IIS по умолчанию для новых публикаций.",
                Kind: SettingValueKind.Text,
                DefaultValue: "Default Web Site"),

            [SettingKey.OneCDefaultPlatformVersion] = new(
                SettingKey.OneCDefaultPlatformVersion,
                IsSecret: false,
                Description: "Версия платформы 1С по умолчанию для новых публикаций (например, 8.3.23.1865).",
                Kind: SettingValueKind.Text),

            [SettingKey.PollingHotIntervalSeconds] = new(
                SettingKey.PollingHotIntervalSeconds,
                IsSecret: false,
                Description: "Период hot-цикла опроса 1С (для tenant'ов у лимита), секунды.",
                Kind: SettingValueKind.Number,
                DefaultValue: "4",
                Min: 2,
                Max: 60),

            [SettingKey.PollingColdIntervalSeconds] = new(
                SettingKey.PollingColdIntervalSeconds,
                IsSecret: false,
                Description: "Период полного cold-снапшота сессий, секунды.",
                Kind: SettingValueKind.Number,
                DefaultValue: "25",
                Min: 10,
                Max: 300),

            [SettingKey.PollingHotThresholdPercent] = new(
                SettingKey.PollingHotThresholdPercent,
                IsSecret: false,
                Description: "Процент заполнения лимита, при котором tenant попадает в hot-tier.",
                Kind: SettingValueKind.Number,
                DefaultValue: "90",
                Min: 50,
                Max: 100),

            [SettingKey.DriftIntervalMinutes] = new(
                SettingKey.DriftIntervalMinutes,
                IsSecret: false,
                Description: "Интервал фонового обновления статуса публикаций в IIS, минуты.",
                Kind: SettingValueKind.Number,
                DefaultValue: "5",
                Min: 1,
                Max: 60),

            [SettingKey.EnforcementKillGraceSeconds] = new(
                SettingKey.EnforcementKillGraceSeconds,
                IsSecret: false,
                Description: "Отсрочка перед завершением только что подключившегося сеанса при превышении лимита, секунды. Пауза даёт 1С определить имя пользователя (иначе в журнале аудита оно пустое) и не завершать сеанс в момент входа.",
                Kind: SettingValueKind.Number,
                DefaultValue: "15",
                Min: 5,
                Max: 120),

            [SettingKey.AuditRetentionDays] = new(
                SettingKey.AuditRetentionDays,
                IsSecret: false,
                Description: "Срок хранения записей аудита в днях. Старые записи удаляются автоматически по ночам.",
                Kind: SettingValueKind.Number,
                DefaultValue: "365",
                Min: 30,
                Max: 3650),

            [SettingKey.LicenseUsageRetentionDays] = new(
                SettingKey.LicenseUsageRetentionDays,
                IsSecret: false,
                Description: "Срок хранения истории использования лицензий (для отчётов) в днях. Старые замеры удаляются автоматически по ночам.",
                Kind: SettingValueKind.Number,
                DefaultValue: "365",
                Min: 30,
                Max: 3650),

            [SettingKey.PerformanceProcessFamilyMap] = new(
                SettingKey.PerformanceProcessFamilyMap,
                IsSecret: false,
                Description: "Маппинг процессов в семьи для раздела «Быстродействие» (атрибуция CPU/RAM). Формат: Семья=маска,маска;Семья2=маска. Имя процесса без .exe, регистр не важен. Пусто → стандартный набор (1С, MSSQL, обновления ОС, антивирус).",
                Kind: SettingValueKind.Text,
                DefaultValue: ProcessFamilyMap.Default),

            [SettingKey.PerformanceRecordingSampleIntervalSeconds] = new(
                SettingKey.PerformanceRecordingSampleIntervalSeconds,
                IsSecret: false,
                Description: "Период сэмплинга активной записи раздела «Быстродействие», секунды. Каждый тик пишет снимок хоста и топ-виновников 1С/SQL в таблицу записи.",
                Kind: SettingValueKind.Number,
                DefaultValue: "15",
                Min: 5,
                Max: 60),

            [SettingKey.PerformanceRecordingMaxDurationMinutes] = new(
                SettingKey.PerformanceRecordingMaxDurationMinutes,
                IsSecret: false,
                Description: "Авто-стоп записи раздела «Быстродействие» по длительности, минуты. Запись останавливается сама по достижении лимита (или лимита числа сэмплов — что раньше).",
                Kind: SettingValueKind.Number,
                DefaultValue: "60",
                Min: 1,
                Max: 1440),

            [SettingKey.PerformanceRecordingMaxSamples] = new(
                SettingKey.PerformanceRecordingMaxSamples,
                IsSecret: false,
                Description: "Авто-стоп записи раздела «Быстродействие» по числу собранных сэмплов. Запись останавливается сама по достижении лимита (или лимита длительности — что раньше).",
                Kind: SettingValueKind.Number,
                DefaultValue: "1000",
                Min: 10,
                Max: 100000),

            // ADR-27: дефолт не сидируем — папка бэкапов зависит от инсталляции
            // (локальный диск SQL-хоста), оператор обязан задать явно через «Параметры»
            // до первого бэкапа (паттерн OneC.RAS.ExePath).
            [SettingKey.BackupFolderPath] = new(
                SettingKey.BackupFolderPath,
                IsSecret: false,
                Description: "Корневая папка для бэкапов баз (локальный диск SQL-сервера, например D:\\Backups). Внутри создаются подпапки по имени базы. Пока не задана — бэкап недоступен.",
                Kind: SettingValueKind.Path),

            [SettingKey.BackupTtlHours] = new(
                SettingKey.BackupTtlHours,
                IsSecret: false,
                Description: "Срок хранения файлов бэкапа в часах. Файлы старше удаляются автоматически по ночам (свежий бэкап каждой базы при штатном цикле один и заменяется при следующем бэкапе).",
                Kind: SettingValueKind.Number,
                DefaultValue: "24",
                Min: 1,
                Max: 8760),

            [SettingKey.BackupMaxParallel] = new(
                SettingKey.BackupMaxParallel,
                IsSecret: false,
                Description: "Максимум одновременных бэкапов на сервере. Лишние запросы ждут в очереди. Перечитывается на каждом тике — действует без рестарта.",
                Kind: SettingValueKind.Number,
                DefaultValue: "2",
                Min: 1,
                Max: 8),

            [SettingKey.BackupDiskSafetyMarginMb] = new(
                SettingKey.BackupDiskSafetyMarginMb,
                IsSecret: false,
                Description: "Запас свободного места (МБ) поверх оценки размера базы, без которого бэкап не стартует (защита от переполнения диска).",
                Kind: SettingValueKind.Number,
                DefaultValue: "2048",
                Min: 0,
                Max: 1048576),
        };
}
