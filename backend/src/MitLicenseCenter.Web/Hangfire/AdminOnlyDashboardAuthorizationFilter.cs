using Hangfire.Dashboard;
using MitLicenseCenter.Infrastructure.Identity;

namespace MitLicenseCenter.Web.Hangfire;

public sealed class AdminOnlyDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;
        return user.Identity?.IsAuthenticated == true && user.IsInRole(Roles.Admin);
    }
}
