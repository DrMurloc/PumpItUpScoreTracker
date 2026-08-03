using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ScoreTracker.Web.Security;

/// <summary>
///     Marks an api/v2 endpoint. Accepts a tool key or a personal token; what each may reach is
///     decided per-resource, not here.
/// </summary>
public sealed class ApiV2Attribute : AuthorizeAttribute
{
    public ApiV2Attribute()
    {
        Policy = nameof(ApiV2Attribute);
        AuthenticationSchemes = ToolKeyAuthenticationScheme.SchemeName;
    }

    public static Task<bool> AuthPolicy(AuthorizationHandlerContext ctx)
    {
        return Task.FromResult(ctx.User.Identity?.IsAuthenticated == true);
    }
}

/// <summary>Reads the caller's identity off the principal the v2 scheme wrote.</summary>
public static class ApiV2Caller
{
    /// <summary>The calling tool, or null when the caller is a person with a personal token.</summary>
    public static Guid? ToolId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ToolKeyAuthenticationScheme.ToolIdClaim);
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
