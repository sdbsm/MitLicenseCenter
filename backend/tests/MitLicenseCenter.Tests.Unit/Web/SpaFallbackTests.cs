using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MitLicenseCenter.Web;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Web;

// MLC-098 — таблица истинности SPA-fallback без загрузки хоста (boot тянет SQL — зеркало
// TransportSecurityTests). /api и /hangfire зарезервированы (отдают свой 404/контракт, не
// index.html); всё прочее (SPA-маршруты) fallback перехватывает и отдаёт оболочку.
public sealed class SpaFallbackTests
{
    [Theory]
    [InlineData("/api")]
    [InlineData("/api/v1/health")]
    [InlineData("/api/v1/unknown")]
    [InlineData("/api/docs")]
    [InlineData("/hangfire")]
    [InlineData("/API/V1/HEALTH")]
    public void Reserved_paths_are_not_intercepted_by_spa_fallback(string path)
    {
        // /api/* (REST + Swagger) и /hangfire отдают свой контракт — fallback их не трогает,
        // иначе неизвестный /api/* подменился бы на index.html (200 HTML вместо честного 404).
        SpaFallback.IsReservedPath(new PathString(path)).Should().BeTrue();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/tenants")]
    [InlineData("/tenants/42")]
    [InlineData("/settings")]
    [InlineData("/applications")]
    [InlineData("/hangfireish")]
    public void Spa_routes_are_intercepted_by_spa_fallback(string path)
    {
        // SPA history-маршруты (включая deep-link) отдаёт fallback. /hangfireish и
        // /applications НЕ зарезервированы — StartsWithSegments сегмент-aware, не префиксный.
        SpaFallback.IsReservedPath(new PathString(path)).Should().BeFalse();
    }
}
