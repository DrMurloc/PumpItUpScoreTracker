using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Web.Accessors;

namespace ScoreTracker.Web.Security;

/// <summary>
///     api/v2's credential resolution. Two kinds of caller arrive here and they are not the same
///     subject:
///     <list type="bullet">
///         <item>
///             a <b>tool key</b> (<c>Authorization: Bearer pst_live_…</c>) authenticates a tool,
///             which acts across the players who granted it access and is never a player itself;
///         </item>
///         <item>
///             a <b>personal token</b> (Basic, a GUID) authenticates one user, exactly as on v1.
///             Unchanged, and only ever resolves that user.
///         </item>
///     </list>
///     <para>
///         The two never overlap: a tool key cannot become a user, and a personal token cannot read
///         another player. The claim written here is what the rest of the pipeline branches on.
///     </para>
/// </summary>
public sealed class ToolKeyAuthenticationScheme : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>Present, and carrying the tool id, exactly when the caller is a tool.</summary>
    public const string ToolIdClaim = "ScoreTracker.ToolId";

    public const string SchemeName = "ApiV2";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public ToolKeyAuthenticationScheme(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock,
        IHttpContextAccessor httpContextAccessor) : base(options, logger, encoder, clock)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return AuthenticateResult.Fail("Not in an HTTP context");

        if (!context.Request.Headers.TryGetValue("Authorization", out var headers) || headers.Count != 1)
            return AuthenticateResult.Fail("Exactly one Authorization header is required");

        var header = headers[0];
        if (string.IsNullOrWhiteSpace(header)) return AuthenticateResult.Fail("Authorization header is empty");

        var mediator = context.RequestServices.GetRequiredService<IMediator>();

        if (header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            var toolId = await mediator.Send(new GetToolByApiKeyQuery(header["Bearer ".Length..].Trim()));
            if (toolId is null) return AuthenticateResult.Fail("API key is not valid");

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ToolIdClaim, toolId.Value.ToString()),
                new Claim(ClaimTypes.Name, toolId.Value.ToString())
            }, SchemeName);
            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }

        if (!header.StartsWith("Basic ", StringComparison.Ordinal))
            return AuthenticateResult.Fail("Authorization must be Bearer (tool key) or Basic (personal token)");

        // Personal tokens are unchanged from v1, down to the iso-8859-1 decode.
        string decoded;
        try
        {
            decoded = Encoding.GetEncoding("iso-8859-1")
                .GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
        }
        catch (Exception)
        {
            return AuthenticateResult.Fail("Could not decode credentials");
        }

        var split = decoded.Split(":");
        if (split.Length != 2 || !Guid.TryParse(split[1], out var apiToken))
            return AuthenticateResult.Fail("Personal token must be a GUID in the password position");

        var user = await mediator.Send(new GetUserByApiTokenQuery(apiToken));
        if (user == null) return AuthenticateResult.Fail("No user has that personal token");

        return AuthenticateResult.Success(
            new AuthenticationTicket(user.GetClaimsPrincipal(), SchemeName));
    }
}
