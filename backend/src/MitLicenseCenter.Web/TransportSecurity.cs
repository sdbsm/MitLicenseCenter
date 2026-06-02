namespace MitLicenseCenter.Web;

// MLC-012 — чистые решения транспортного хардненинга, вынесенные из Program.cs, чтобы их
// таблицу истинности можно было проверить юнит-тестом БЕЗ загрузки хоста (полный boot
// тянет SQL через Hangfire SqlServerStorage + fail-fast bootstrap-миграции). Program.cs
// связывает эти решения с живым пайплайном; тесты — с in-memory IConfiguration.
internal static class TransportSecurity
{
    public const string EnforceHttpsKey = "Security:EnforceHttps";
    public const string EnableSwaggerKey = "Security:EnableSwagger";

    // HSTS + HTTPS-redirect. НИКОГДА в Development (dev ходит по http к локальному SQL без
    // TLS); вне Development — только когда оператор сам включил Security:EnforceHttps
    // (true ставится лишь если сервис сам терминирует TLS — перед ним нет терминирующего
    // реверс-прокси, иначе redirect/HSTS уже делает прокси и дублировать нельзя).
    public static bool ShouldEnforceHttps(bool isDevelopment, IConfiguration config)
        => !isDevelopment && config.GetValue<bool>(EnforceHttpsKey);

    // Swagger UI. Всегда в Development (на нём держится ручная синхронизация TS-типов —
    // ADR-10.1); в любом другом окружении — только если Security:EnableSwagger=true
    // возвращает его для внутреннего admin-only периметра.
    public static bool ShouldEnableSwagger(bool isDevelopment, IConfiguration config)
        => isDevelopment || config.GetValue<bool>(EnableSwaggerKey);
}
