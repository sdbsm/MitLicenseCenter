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
            [SettingKey.OneCClusterRestApiUrl] = new(
                SettingKey.OneCClusterRestApiUrl,
                IsSecret: false,
                Description: "Базовый URL 1С Cluster REST API (http/https, абсолютный).",
                Kind: SettingValueKind.Url),

            [SettingKey.OneCClusterAdminUser] = new(
                SettingKey.OneCClusterAdminUser,
                IsSecret: false,
                Description: "Логин администратора кластера 1С (Basic-auth для REST API).",
                Kind: SettingValueKind.Text),

            [SettingKey.OneCClusterAdminPassword] = new(
                SettingKey.OneCClusterAdminPassword,
                IsSecret: true,
                Description: "Пароль администратора кластера 1С. Хранится зашифрованным DPAPI.",
                Kind: SettingValueKind.Text),

            [SettingKey.OneCClusterRestApiTimeoutSeconds] = new(
                SettingKey.OneCClusterRestApiTimeoutSeconds,
                IsSecret: false,
                Description: "Тайм-аут одного REST-вызова к 1С Cluster, секунды.",
                Kind: SettingValueKind.Number,
                DefaultValue: "5",
                Min: 1,
                Max: 30),

            [SettingKey.OneCRasEndpoint] = new(
                SettingKey.OneCRasEndpoint,
                IsSecret: false,
                Description: "Endpoint RAS-сервера в формате host:port (используется как fallback).",
                Kind: SettingValueKind.HostPort),

            // ADR-3.3: дефолт не сидируем — 1С 8.5 положил rac.exe в версионную
            // папку (`1cv8\<version>\bin\`), в `1cv8\common\` его нет. Оператор
            // обязан задать явно через «Параметры» при настройке RAS fallback'а.
            [SettingKey.OneCRasExePath] = new(
                SettingKey.OneCRasExePath,
                IsSecret: false,
                Description: "Путь к rac.exe для RAS fallback (например, C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\rac.exe).",
                Kind: SettingValueKind.Path),

            [SettingKey.IisServiceAccountUserName] = new(
                SettingKey.IisServiceAccountUserName,
                IsSecret: false,
                Description: "Учётка, под которой работает service IIS (informational, для подсказок).",
                Kind: SettingValueKind.Text),

            [SettingKey.IisDefaultVrdRoot] = new(
                SettingKey.IisDefaultVrdRoot,
                IsSecret: false,
                Description: "Базовый каталог публикаций 1С на IIS.",
                Kind: SettingValueKind.Path,
                DefaultValue: @"C:\inetpub\1c-publications"),

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
                Description: "Интервал проверки дрейфа публикаций в IIS, минуты.",
                Kind: SettingValueKind.Number,
                DefaultValue: "5",
                Min: 1,
                Max: 60),

            [SettingKey.CircuitBreakerProbeIntervalSeconds] = new(
                SettingKey.CircuitBreakerProbeIntervalSeconds,
                IsSecret: false,
                Description: "Интервал probe-вызова REST после открытия circuit breaker, секунды.",
                Kind: SettingValueKind.Number,
                DefaultValue: "60",
                Min: 10,
                Max: 300),

            [SettingKey.CircuitBreakerFailureCount] = new(
                SettingKey.CircuitBreakerFailureCount,
                IsSecret: false,
                Description: "Сколько подряд ошибок REST размыкает circuit breaker.",
                Kind: SettingValueKind.Number,
                DefaultValue: "3",
                Min: 2,
                Max: 10),

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
