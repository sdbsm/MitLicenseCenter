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

// MLC-077 (ADR-27): роль-гейт бэкапов через метаданные смонтированных маршрутов (образец
// PerformanceAuthorizationTests). Запуск бэкапа — операторская кнопка, поэтому POST = Viewer;
// удаление (сносит .bak server-side) = Admin.
public sealed class BackupsAuthorizationTests
{
    [Theory]
    [InlineData("GET", "backups", Roles.Viewer)]
    [InlineData("GET", "backups/{id:guid}", Roles.Viewer)]
    [InlineData("POST", "backups", Roles.Viewer)]
    [InlineData("DELETE", "backups/{id:guid}", Roles.Admin)]
    public void Endpoint_requires_expected_role(string method, string suffix, string expectedPolicy)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApiVersioning().AddApiExplorer();
        builder.Services.AddAuthorization();
        using var app = builder.Build();

        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1, 0)).Build();
        app.MapBackupsEndpoints(versionSet);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(d => d.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var endpoint = endpoints.Single(e =>
            e.RoutePattern.RawText!.TrimEnd('/').EndsWith(suffix, StringComparison.Ordinal) &&
            (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) ?? false));

        endpoint.Metadata.GetMetadata<IAuthorizeData>()!.Policy.Should().Be(expectedPolicy);
    }
}
