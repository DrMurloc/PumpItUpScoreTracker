using Microsoft.AspNetCore.Authorization;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Security
{
    public class ApiTokenAttribute : AuthorizeAttribute
    {
        public ApiTokenAttribute()
        {
            Policy = nameof(ApiTokenAttribute);
            AuthenticationSchemes = "ApiToken";
        }

        public static Task<bool> AuthPolicy(AuthorizationHandlerContext ctx)
        {
            return Task.FromResult(ctx.Resource is HttpContext httpContext &&
                                   httpContext.RequestServices.GetRequiredService<ICurrentUserAccessor>().IsLoggedIn);
        }
    }
}
