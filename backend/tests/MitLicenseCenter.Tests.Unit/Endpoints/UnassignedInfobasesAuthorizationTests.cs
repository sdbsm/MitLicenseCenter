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

// MLC-092: весь слайс «нераспределённых» — Admin-only (Viewer не видит ничего нового,
// endpoint для него закрыт → 403: политика Admin требует роль Admin, политика Viewer
// в Program.cs допускает обе). Декларативный `.RequireAuthorization` проверяем через
// метаданные смонтированных маршрутов (образец PerformanceAuthorizationTests).
public sealed class UnassignedInfobasesAuthorizationTests
{
    [Theory]
    [InlineData("GET", "infobases/unassigned", Roles.Admin)]
    [InlineData("POST", "{clusterInfobaseId:guid}/hide", Roles.Admin)]
    [InlineData("DELETE", "{clusterInfobaseId:guid}/hide", Roles.Admin)]
    public void Endpoint_requires_admin_policy(string method, string suffix, string expectedPolicy)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddApiVersioning().AddApiExplorer();
        builder.Services.AddAuthorization();
        using var app = builder.Build();

        var versionSet = app.NewApiVersionSet().HasApiVersion(new ApiVersion(1, 0)).Build();
        app.MapUnassignedInfobasesEndpoints(versionSet);

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
