namespace MitLicenseCenter.Web.Security;

// MLC-125 (SEC-07) — Security response headers. Ставятся на все ответы (SPA, статика,
// API, fallback) ранним middleware сразу после UseExceptionHandler. Swagger (/api/docs)
// исключён из CSP — SwaggerUI использует inline-скрипты, строгий script-src его сломает;
// прочие заголовки (nosniff, Referrer-Policy) на Swagger оставляются.
//
// Вынесено из Program.cs как отдельный класс — чтобы логика исключения Swagger
// была unit-тестируемой без загрузки хоста.
internal static class SecurityHeaders
{
    // Путь Swagger UI. CSP-исключение безусловное по префиксу — проще и безопасно:
    // этот путь зарезервирован только под Swagger.
    private const string SwaggerPrefix = "/api/docs";

    // CSP (ADR-41): same-origin SPA (ADR-30). Значения сшиты жёстко — single-host,
    // SPA-бандл известен:
    //   • script-src 'self' — без unsafe-inline (Vite-бандл не содержит inline-script'ов).
    //   • style-src 'self' 'unsafe-inline' — нужен для React/CSS-in-JS инлайновых стилей.
    //   • img-src / font-src — 'self' + data: (иконки/шрифты могут быть base64 data URI).
    //   • frame-ancestors 'none' дублирует X-Frame-Options DENY для современных браузеров.
    //   • object-src 'none' — запрет Flash/embed.
    //   • connect-src 'self' — XHR/fetch только к своему origin (API same-origin).
    private static readonly string CspValue = string.Join("; ", new[]
    {
        "default-src 'self'",
        "base-uri 'self'",
        "object-src 'none'",
        "frame-ancestors 'none'",
        "img-src 'self' data:",
        "font-src 'self' data:",
        "style-src 'self' 'unsafe-inline'",
        "script-src 'self'",
        "connect-src 'self'",
    });

    /// <summary>
    /// Регистрирует middleware, ставящий security-заголовки на все ответы.
    /// CSP исключается на путях /api/docs/* (Swagger UI).
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            ApplyHeaders(context);
            await next(context).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Применяет security-заголовки к данному <see cref="HttpContext"/>.
    /// Вынесено для unit-тестирования без HTTP-стека.
    /// </summary>
    internal static void ApplyHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Ставим на все пути (включая /api/docs):
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";

        // CSP и X-Frame-Options — не на Swagger, чтобы не сломать inline-скрипты SwaggerUI:
        if (!IsSwaggerPath(context.Request.Path))
        {
            headers["Content-Security-Policy"] = CspValue;
            headers["X-Frame-Options"] = "DENY";
        }
    }

    /// <summary>Проверяет, является ли путь Swagger-путём (исключение из CSP).</summary>
    internal static bool IsSwaggerPath(PathString path)
        => path.StartsWithSegments(SwaggerPrefix, StringComparison.OrdinalIgnoreCase);
}
