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

            [SettingKey.OneCClusterServer] = new(
                SettingKey.OneCClusterServer,
                IsSecret: false,
                Description: "Адрес 1С-кластера для публикации через webinst (строка соединения Srvr=…;Ref=…). Формат host или host:port. Пусто → берётся host из OneC.RAS.Endpoint.",
                Kind: SettingValueKind.Text),

            // Дефолты формы добавления инфобазы — подставляются как значения по
            // умолчанию в новую базу, чтобы не вводить одинаковое каждый раз.
            // На бекенде не используются (форма-only), но живут в общем каталоге
            // настроек, чтобы их можно было задать через UI «Параметры».
            [SettingKey.DefaultsDatabaseServer] = new(
                SettingKey.DefaultsDatabaseServer,
                IsSecret: false,
                Description: "SQL-сервер по умолчанию для новых инфобаз (например, sql.local или (local)).",
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

            [SettingKey.AuditRetentionDays] = new(
                SettingKey.AuditRetentionDays,
                IsSecret: false,
                Description: "Срок хранения записей аудита в днях. Старые записи удаляются автоматически по ночам.",
                Kind: SettingValueKind.Number,
                DefaultValue: "365",
                Min: 30,
                Max: 3650),
        };
}
