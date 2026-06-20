using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MitLicenseCenter.Web;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Web;

// MLC-012 — smoke по конфигу транспортного хардненинга без загрузки хоста (boot тянет SQL).
// Проверяем ровно те решения, что Program.cs подаёт в пайплайн: HSTS/HTTPS-redirect и
// гейт Swagger. dev по http и видит Swagger; prod-профиль форсит https и закрывает Swagger.
public sealed class TransportSecurityTests
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p =>
                new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    // ── HSTS + HTTPS-redirect ──────────────────────────────────────────────────────

    [Fact]
    public void EnforceHttps_off_in_Development_even_when_flag_true()
    {
        // dev стартует по http к локальному SQL без TLS — флаг не должен это ломать.
        var config = Config((TransportSecurity.EnforceHttpsKey, "true"));
        TransportSecurity.ShouldEnforceHttps(isDevelopment: true, config).Should().BeFalse();
    }

    [Fact]
    public void EnforceHttps_off_in_Production_by_default()
    {
        // Дефолт (за терминирующим прокси) — приложение не дублирует redirect/HSTS.
        var config = Config();
        TransportSecurity.ShouldEnforceHttps(isDevelopment: false, config).Should().BeFalse();
    }

    [Fact]
    public void EnforceHttps_on_in_Production_when_flag_true()
    {
        // Сервис сам терминирует TLS (нет прокси) — оператор включил флаг.
        var config = Config((TransportSecurity.EnforceHttpsKey, "true"));
        TransportSecurity.ShouldEnforceHttps(isDevelopment: false, config).Should().BeTrue();
    }

    // ── Swagger gate ───────────────────────────────────────────────────────────────

    [Fact]
    public void Swagger_on_in_Development_by_default()
    {
        // На Swagger держится ручная синхронизация TS-типов (ADR-10.1).
        var config = Config();
        TransportSecurity.ShouldEnableSwagger(isDevelopment: true, config).Should().BeTrue();
    }

    [Fact]
    public void Swagger_off_in_Production_by_default()
    {
        var config = Config();
        TransportSecurity.ShouldEnableSwagger(isDevelopment: false, config).Should().BeFalse();
    }

    [Fact]
    public void Swagger_on_in_Production_when_override_flag_true()
    {
        // Override для внутреннего admin-only периметра.
        var config = Config((TransportSecurity.EnableSwaggerKey, "true"));
        TransportSecurity.ShouldEnableSwagger(isDevelopment: false, config).Should().BeTrue();
    }

    // ── SecurePolicy auth-куки (ADR-59) ─────────────────────────────────────────────

    [Fact]
    public void AuthCookie_SameAsRequest_in_Development()
    {
        // dev по http — Always выбросил бы куку на не-localhost.
        var config = Config((TransportSecurity.RequireSecureCookieKey, "true"));
        TransportSecurity.AuthCookieSecurePolicy(isDevelopment: true, config)
            .Should().Be(CookieSecurePolicy.SameAsRequest);
    }

    [Fact]
    public void AuthCookie_SameAsRequest_in_Production_by_default()
    {
        // Штатный http-LAN мастера ("Urls":"http://+:port", EnforceHttps=false): без
        // SameAsRequest вход из локальной сети невозможен (регресс — баг MLC-240).
        var config = Config();
        TransportSecurity.AuthCookieSecurePolicy(isDevelopment: false, config)
            .Should().Be(CookieSecurePolicy.SameAsRequest);
    }

    [Fact]
    public void AuthCookie_Always_in_Production_when_EnforceHttps_true()
    {
        // Сервис сам терминирует TLS — кука обязана быть Secure.
        var config = Config((TransportSecurity.EnforceHttpsKey, "true"));
        TransportSecurity.AuthCookieSecurePolicy(isDevelopment: false, config)
            .Should().Be(CookieSecurePolicy.Always);
    }

    [Fact]
    public void AuthCookie_Always_in_Production_when_RequireSecureCookie_true()
    {
        // За TLS-реверс-прокси (EnforceHttps=false, redirect/HSTS делает прокси): оператор
        // явно сохраняет Secure-куку, хотя Kestrel видит http от прокси.
        var config = Config((TransportSecurity.RequireSecureCookieKey, "true"));
        TransportSecurity.AuthCookieSecurePolicy(isDevelopment: false, config)
            .Should().Be(CookieSecurePolicy.Always);
    }
}
