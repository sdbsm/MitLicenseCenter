using Asp.Versioning;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MitLicenseCenter.Infrastructure.Identity;
using MitLicenseCenter.Web.Endpoints;
using Xunit;

namespace MitLicenseCenter.Tests.Unit.Endpoints;

// MLC-070 (ADR-26): роль-гейт раздела «Быстродействие». Декларативный `.RequireAuthorization`
// проверяем через метаданные смонтированных маршрутов: live-чтение и просмотр записей = Viewer,
// управление записью (старт/стоп/удаление) = Admin.
public sealed class PerformanceAuthorizationTests
{
    [Theory]
    [InlineData("GET", "/host", Roles.Viewer)]
    [InlineData("GET", "/onec-sessions", Roles.Viewer)]
    [InlineData("GET", "/sql", Roles.Viewer)]
    [InlineData("GET", "/recordings", Roles.Viewer)]
    [InlineData("GET", "/recordings/{id:guid}", Roles.Viewer)]
    [InlineData("POST", "/recordings", Roles.Admin)]
    [InlineData("POST", "/recordings/{id:guid}/stop", Roles.Admin)]
    [InlineData("DELETE", "/recordings/{id:guid}", Roles.Admin)]
    public void Endpoint_requires_expected_role(string method, string suffix, string expectedPolicy)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApiVersioning().AddApiExplorer();
        builder.Services.AddAuthorization();
        using var app = builder.Build();

        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1, 0)).Build();
        app.MapPerformanceEndpoints(versionSet);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(d => d.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var endpoint = endpoints.Single(e =>
            e.RoutePattern.RawText!.EndsWith(suffix, StringComparison.Ordinal) &&
            (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) ?? false));

        endpoint.Metadata.GetMetadata<IAuthorizeData>()!.Policy.Should().Be(expectedPolicy);
    }
}
