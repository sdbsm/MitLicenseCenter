using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MitLicenseCenter.Web.Security;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Web;

// MLC-125 (SEC-07) — юнит-тесты для SecurityHeaders middleware.
// Проверяем ApplyHeaders и IsSwaggerPath напрямую через DefaultHttpContext — без загрузки хоста.
// Интеграционные тесты (SecurityHeadersIntegrationTests) проверяют реальный HTTP-пайплайн.
public sealed class SecurityHeadersTests
{
    // ── IsSwaggerPath ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/docs")]
    [InlineData("/api/docs/")]
    [InlineData("/api/docs/index.html")]
    [InlineData("/api/docs/v1/swagger.json")]
    [InlineData("/API/DOCS/anything")]
    public void IsSwaggerPath_returns_true_for_swagger_paths(string path)
        => SecurityHeaders.IsSwaggerPath(path).Should().BeTrue(
            "путь {0} начинается с /api/docs — CSP-исключение", path);

    [Theory]
    [InlineData("/")]
    [InlineData("/api/v1/health")]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/hangfire")]
    [InlineData("/api/docs-extra")]    // не начинается с /api/docs как сегмент
    public void IsSwaggerPath_returns_false_for_non_swagger_paths(string path)
        => SecurityHeaders.IsSwaggerPath(path).Should().BeFalse(
            "путь {0} не является Swagger-путём", path);

    // ── ApplyHeaders — обычный путь ────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyHeaders_sets_nosniff_and_referrer_on_all_paths()
    {
        var ctx = MakeContext("/api/v1/health");
        SecurityHeaders.ApplyHeaders(ctx);

        ctx.Response.Headers["X-Content-Type-Options"].ToString()
            .Should().Be("nosniff");
        ctx.Response.Headers["Referrer-Policy"].ToString()
            .Should().Be("no-referrer");
    }

    [Fact]
    public void ApplyHeaders_sets_csp_and_xfo_on_non_swagger_path()
    {
        var ctx = MakeContext("/api/v1/users");
        SecurityHeaders.ApplyHeaders(ctx);

        var csp = ctx.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("default-src 'self'", "CSP содержит default-src");
        csp.Should().Contain("frame-ancestors 'none'", "CSP содержит frame-ancestors");
        csp.Should().Contain("object-src 'none'", "CSP содержит object-src");
        csp.Should().Contain("script-src 'self'", "CSP содержит script-src без unsafe-inline");

        ctx.Response.Headers["X-Frame-Options"].ToString()
            .Should().Be("DENY");
    }

    // ── ApplyHeaders — Swagger-исключение ─────────────────────────────────────────────────────

    [Fact]
    public void ApplyHeaders_omits_csp_and_xfo_on_swagger_path()
    {
        var ctx = MakeContext("/api/docs/index.html");
        SecurityHeaders.ApplyHeaders(ctx);

        // CSP и X-Frame-Options НЕ ставятся на /api/docs — SwaggerUI использует inline-скрипты
        ctx.Response.Headers.ContainsKey("Content-Security-Policy")
            .Should().BeFalse("CSP ломает SwaggerUI на /api/docs");
        ctx.Response.Headers.ContainsKey("X-Frame-Options")
            .Should().BeFalse("X-Frame-Options также исключён на Swagger-пути");
    }

    [Fact]
    public void ApplyHeaders_keeps_nosniff_and_referrer_on_swagger_path()
    {
        // nosniff и Referrer-Policy ставятся даже на Swagger — они не ломают SwaggerUI
        var ctx = MakeContext("/api/docs/v1/swagger.json");
        SecurityHeaders.ApplyHeaders(ctx);

        ctx.Response.Headers["X-Content-Type-Options"].ToString()
            .Should().Be("nosniff");
        ctx.Response.Headers["Referrer-Policy"].ToString()
            .Should().Be("no-referrer");
    }

    // ── CSP-значения — детальная проверка ─────────────────────────────────────────────────────

    [Fact]
    public void ApplyHeaders_csp_allows_inline_styles_for_react()
    {
        // style-src 'unsafe-inline' нужен для React/CSS-in-JS инлайновых стилей (ADR-41)
        var ctx = MakeContext("/");
        SecurityHeaders.ApplyHeaders(ctx);

        var csp = ctx.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("style-src 'self' 'unsafe-inline'",
            "style-src должен разрешать unsafe-inline для React/CSS-in-JS");
    }

    [Fact]
    public void ApplyHeaders_csp_does_not_allow_unsafe_inline_scripts()
    {
        // script-src должен быть строгим: только 'self', без unsafe-inline
        var ctx = MakeContext("/");
        SecurityHeaders.ApplyHeaders(ctx);

        var csp = ctx.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("script-src 'self'");
        csp.Should().NotContain("script-src 'self' 'unsafe-inline'",
            "inline-скрипты в SPA-бандле не используются — unsafe-inline лишний");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private static DefaultHttpContext MakeContext(string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        return ctx;
    }
}
