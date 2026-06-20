namespace MitLicenseCenter.Web;

// MLC-012 — чистые решения транспортного хардненинга, вынесенные из Program.cs, чтобы их
// таблицу истинности можно было проверить юнит-тестом БЕЗ загрузки хоста (полный boot
// тянет SQL через Hangfire SqlServerStorage + fail-fast bootstrap-миграции). Program.cs
// связывает эти решения с живым пайплайном; тесты — с in-memory IConfiguration.
internal static class TransportSecurity
{
    public const string EnforceHttpsKey = "Security:EnforceHttps";
    public const string EnableSwaggerKey = "Security:EnableSwagger";
    public const string RequireSecureCookieKey = "Security:RequireSecureCookie";

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

    // SecurePolicy auth-куки «mlc.auth» (ADR-59). Always помечает куку флагом Secure — браузер
    // хранит/шлёт её ТОЛЬКО по HTTPS, в т.ч. на http://localhost (защищённый контекст), но НЕ на
    // http://<имя-хоста> из LAN → при штатном http-деплое мастера ("Urls":"http://+:port",
    // EnforceHttps=false) вход из сети физически невозможен (кука выбрасывается). Поэтому секьюрность
    // куки следует за транспортом:
    //   • Development → SameAsRequest (dev по http);
    //   • Always — если приложение само терминирует TLS (EnforceHttps=true) ИЛИ оператор за
    //     терминирующим TLS-реверс-прокси явно включил RequireSecureCookie=true;
    //   • иначе SameAsRequest — типовой http-LAN, где Secure-флаг защиты не даёт (TLS нет вовсе),
    //     а вход из сети ломает. SameAsRequest сам ставит Secure на запросах по https.
    public static CookieSecurePolicy AuthCookieSecurePolicy(bool isDevelopment, IConfiguration config)
        => !isDevelopment
            && (ShouldEnforceHttps(isDevelopment, config) || config.GetValue<bool>(RequireSecureCookieKey))
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
}
