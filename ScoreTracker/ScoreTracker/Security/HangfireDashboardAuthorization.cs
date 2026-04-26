using Hangfire.Dashboard;
using ScoreTracker.Web.Accessors;

namespace ScoreTracker.Web.Security;

public sealed class HangfireDashboardAuthorization : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var principal = httpContext.User;
        if (principal.Identity?.IsAuthenticated != true) return false;
        return principal.GetUser().IsAdmin;
    }
}
