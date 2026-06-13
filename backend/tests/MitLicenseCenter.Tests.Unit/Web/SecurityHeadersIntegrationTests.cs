using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Web;

// MLC-125 (SEC-07) — интеграционные тесты security response headers через HTTP-пайплайн.
// Проверяем, что UseSecurityHeaders middleware ставит нужные заголовки на реальные HTTP-ответы.
// Используем MlcWebApplicationFactory (EF InMemory + Hangfire stub, без реального SQL Server).
[Collection("WebApp")]
public sealed class SecurityHeadersIntegrationTests : IClassFixture<MlcWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersIntegrationTests(MlcWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // Не следуем за редиректами — нас интересуют заголовки любого ответа
            AllowAutoRedirect = false,
        });
    }

    // ── Базовые 4 заголовка на публичном пути ────────────────────────────────────────────────

    [Fact]
    public async Task Get_health_returns_x_content_type_options_nosniff()
    {
        var response = await _client.GetAsync("/api/v1/health");

        response.Headers.TryGetValues("X-Content-Type-Options", out var values).Should().BeTrue(
            "X-Content-Type-Options должен быть на всех ответах");
        values!.First().Should().Be("nosniff");
    }

    [Fact]
    public async Task Get_health_returns_referrer_policy_no_referrer()
    {
        var response = await _client.GetAsync("/api/v1/health");

        response.Headers.TryGetValues("Referrer-Policy", out var values).Should().BeTrue(
            "Referrer-Policy должен быть на всех ответах");
        values!.First().Should().Be("no-referrer");
    }

    [Fact]
    public async Task Get_health_returns_x_frame_options_deny()
    {
        var response = await _client.GetAsync("/api/v1/health");

        response.Headers.TryGetValues("X-Frame-Options", out var values).Should().BeTrue(
            "X-Frame-Options должен быть на API-ответах");
        values!.First().Should().Be("DENY");
    }

    [Fact]
    public async Task Get_health_returns_csp_with_required_directives()
    {
        var response = await _client.GetAsync("/api/v1/health");

        response.Headers.TryGetValues("Content-Security-Policy", out var values).Should().BeTrue(
            "CSP должен быть на API-ответах");

        var csp = values!.First();
        csp.Should().Contain("default-src 'self'", "CSP должен содержать default-src");
        csp.Should().Contain("frame-ancestors 'none'", "CSP должен содержать frame-ancestors");
        csp.Should().Contain("object-src 'none'", "CSP должен содержать object-src");
        csp.Should().Contain("script-src 'self'", "CSP должен содержать script-src");
    }

    // ── Проверка конкретных CSP-значений для SPA ─────────────────────────────────────────────

    [Fact]
    public async Task Get_api_csp_allows_inline_styles_for_react_components()
    {
        var response = await _client.GetAsync("/api/v1/health");
        var csp = response.Headers.GetValues("Content-Security-Policy").First();

        // style-src 'unsafe-inline' нужен для React/CSS-in-JS (ADR-41)
        csp.Should().Contain("style-src 'self' 'unsafe-inline'",
            "React-компоненты используют инлайновые стили");
    }

    [Fact]
    public async Task Get_api_csp_does_not_have_unsafe_inline_in_script_src()
    {
        var response = await _client.GetAsync("/api/v1/health");
        var csp = response.Headers.GetValues("Content-Security-Policy").First();

        // Vite-бандл в проде не содержит inline-script — unsafe-inline лишний и небезопасен
        csp.Should().NotContain("script-src 'self' 'unsafe-inline'",
            "script-src не должен разрешать inline-скрипты");
    }
}
