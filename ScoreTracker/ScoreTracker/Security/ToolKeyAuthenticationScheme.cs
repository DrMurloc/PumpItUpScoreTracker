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
///             a <b>tool key</b> (<c>Authorization: Bearer piu_scores_live_…</c>) authenticates a
///             tool, which acts across the players who granted it access and is never a player
///             itself;
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

            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(ToolIdentity(toolId.Value)), SchemeName));
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
        if (split.Length != 2)
            return AuthenticateResult.Fail("Basic credentials must be username:password");

        // A tool key in the password position resolves as a tool. Bearer is what the docs say and
        // what Swagger sends, but v1 taught every integrator here that a credential goes in the
        // Basic password box with junk for the username — so that is the first thing they try, and
        // failing it teaches them nothing except that their new key is broken. Same key material,
        // same TLS, same claim: the only thing rejecting it bought was a support question.
        if (!Guid.TryParse(split[1], out var apiToken))
        {
            var toolByBasic = await mediator.Send(new GetToolByApiKeyQuery(split[1]));
            if (toolByBasic is null)
                return AuthenticateResult.Fail(
                    "Password must be a personal token (a GUID) or a tool API key");

            return AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(ToolIdentity(toolByBasic.Value)), SchemeName));
        }

        var user = await mediator.Send(new GetUserByApiTokenQuery(apiToken));
        if (user == null) return AuthenticateResult.Fail("No user has that personal token");

        return AuthenticateResult.Success(
            new AuthenticationTicket(user.GetClaimsPrincipal(), SchemeName));
    }

    /// <summary>A tool, whichever header carried its key. Never a user.</summary>
    private static ClaimsIdentity ToolIdentity(Guid toolId)
    {
        return new ClaimsIdentity(new[]
        {
            new Claim(ToolIdClaim, toolId.ToString()),
            new Claim(ClaimTypes.Name, toolId.ToString())
        }, SchemeName);
    }
}
