using System.Security.Claims;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.Models;
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

    /// <summary>
    ///     The name the maker gave the key that authenticated this call. Beside the tool id rather
    ///     than folded into it: a tool has two live keys so rotation costs no downtime, and a count
    ///     that cannot say which key it belongs to is half a number.
    /// </summary>
    public const string KeyNameClaim = "ScoreTracker.ToolKeyName";

    /// <summary>
    ///     Present exactly when a person authenticated with a personal token, on v1 or v2. The
    ///     cookie principal a signed-in browser carries into an API route is built from the same
    ///     claims, and the request trace has to tell a token from a session.
    /// </summary>
    public const string PersonalTokenClaim = "ScoreTracker.PersonalToken";

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

        var credential = ApiCredential.Parse(headers[0]);
        if (credential.Failure is not null) return AuthenticateResult.Fail(credential.Failure);

        var mediator = context.RequestServices.GetRequiredService<IMediator>();

        if (credential.Kind == ApiCredentialKind.Bearer)
        {
            var tool = await mediator.Send(new GetToolByApiKeyQuery(credential.Secret));
            if (tool is null) return AuthenticateResult.Fail("API key is not valid");

            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(ToolIdentity(tool)), SchemeName));
        }

        // A tool key in the password position resolves as a tool. Bearer is what the docs say and
        // what Swagger sends, but v1 taught every integrator here that a credential goes in the
        // Basic password box with junk for the username — so that is the first thing they try, and
        // failing it teaches them nothing except that their new key is broken. Same key material,
        // same TLS, same claim: the only thing rejecting it bought was a support question.
        if (!Guid.TryParse(credential.Secret, out var apiToken))
        {
            var toolByBasic = await mediator.Send(new GetToolByApiKeyQuery(credential.Secret));
            if (toolByBasic is null)
                return AuthenticateResult.Fail(
                    "Password must be a personal token (a GUID) or a tool API key");

            return AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(ToolIdentity(toolByBasic)), SchemeName));
        }

        var user = await mediator.Send(new GetUserByApiTokenQuery(apiToken));
        if (user == null) return AuthenticateResult.Fail("No user has that personal token");

        return AuthenticateResult.Success(new AuthenticationTicket(PersonalIdentity(user), SchemeName));
    }

    /// <summary>A person, by their token — the site's claims plus the mark that says a token brought them.</summary>
    public static ClaimsPrincipal PersonalIdentity(User user)
    {
        var principal = user.GetClaimsPrincipal();
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(PersonalTokenClaim, "true"));
        return principal;
    }

    /// <summary>A tool, whichever header carried its key. Never a user.</summary>
    private static ClaimsIdentity ToolIdentity(ToolKeyPrincipal tool)
    {
        return new ClaimsIdentity(new[]
        {
            new Claim(ToolIdClaim, tool.ToolId.ToString()),
            new Claim(KeyNameClaim, tool.KeyName),
            new Claim(ClaimTypes.Name, tool.ToolId.ToString())
        }, SchemeName);
    }
}
