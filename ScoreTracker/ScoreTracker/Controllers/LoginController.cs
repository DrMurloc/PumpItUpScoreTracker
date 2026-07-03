using System.Security.Authentication;
using System.Security.Claims;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Configuration;
using ScoreTracker.Web.Services.Contracts;

namespace ScoreTracker.Web.Controllers;

[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class LoginController : Controller
{
    private static readonly ISet<string> AllowedProviders =
        new HashSet<string>(new[] { "Discord", "Google", "Facebook" }, StringComparer.OrdinalIgnoreCase);

    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly IUiSettingsAccessor _uiSettings;
    private readonly IOptions<DevAuthConfiguration> _devAuth;

    public LoginController(IMediator mediator,
        ICurrentUserAccessor currentUser,
        IUiSettingsAccessor uiSettings,
        IOptions<DevAuthConfiguration> devAuth)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _uiSettings = uiSettings;
        _devAuth = devAuth;
    }


    [HttpGet("{providerName}")]
    public async Task<IActionResult> SignIn([FromRoute] string providerName)
    {
        if (!AllowedProviders.Contains(providerName)) return BadRequest("Invalid provider name");


        if (!await HttpContext.IsProviderSupportedAsync(providerName)) return BadRequest();

        return Challenge(new AuthenticationProperties { RedirectUri = $"/Login/{providerName}/Callback" },
            providerName);
    }

    [HttpGet("{providerName}/Callback")]
    public async Task<IActionResult> ExternalCallback([FromRoute] string providerName)
    {
        if (!AllowedProviders.Contains(providerName)) return BadRequest("Invalid provider name");

        var authenticateResult = await HttpContext.AuthenticateAsync(providerName);

        if (authenticateResult.Principal == null) return BadRequest("Principal was missing");

        var returnUrl = "";
        authenticateResult.Ticket?.Properties.Items.TryGetValue("returnUrl", out returnUrl);

        var principal = authenticateResult.Principal;

        var id = principal.FindFirst(ClaimTypes.NameIdentifier)
            ?.Value ?? "";
        var name = principal.FindFirst(ClaimTypes.Name)?.Value ??
                   "Unknown Name";
        await HttpContext.SignOutAsync("ExternalAuthentication");

        var user = await _mediator.Send(new GetUserByExternalLoginQuery(id, providerName),
            HttpContext.RequestAborted);

        var isNewUser = false;

        if (user == null)
        {
            isNewUser = true;
            user = await _mediator.Send(new CreateUserCommand(name), HttpContext.RequestAborted);
            await _mediator.Send(new CreateExternalLoginCommand(user.Id, id, providerName),
                HttpContext.RequestAborted);
        }

        await _currentUser.SetCurrentUser(user);

        var culture = await _uiSettings.GetSetting("Culture", HttpContext.RequestAborted, user.Id);
        if (culture != null)
            HttpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(
                    new RequestCulture(culture, culture)));
        var url = isNewUser ? "/Welcome" : returnUrl;

        return LocalRedirect(url ?? "/");
    }

    // PIUGAME is credential-based, not OAuth: the literal routes below win over the
    // {providerName} templates, so it never enters the Challenge pipeline. The GET only
    // redirects to the Blazor form; the POST is the actual sign-in (cookies can only be
    // issued from an HTTP request, never over the Blazor circuit).
    [HttpGet("PiuGame")]
    public IActionResult PiuGameForm()
    {
        return LocalRedirect("/PiuGameLogin");
    }

    [HttpPost("PiuGame")]
    public async Task<IActionResult> PiuGameLogin([FromForm] string? username, [FromForm] string? password,
        [FromForm] string? returnUrl)
    {
        var backToForm = "/PiuGameLogin" + (string.IsNullOrWhiteSpace(returnUrl)
            ? ""
            : $"?returnUrl={Uri.EscapeDataString(returnUrl)}");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return LocalRedirect(AppendError(backToForm, "Invalid"));

        OfficialMirror.Contracts.PiuGameAccountIdentity identity;
        try
        {
            identity = await _mediator.Send(new GetPiuGameAccountIdentityQuery(username, password),
                HttpContext.RequestAborted);
        }
        catch (InvalidCredentialException)
        {
            return LocalRedirect(AppendError(backToForm, "Invalid"));
        }
        catch (HttpRequestException)
        {
            return LocalRedirect(AppendError(backToForm, "Unavailable"));
        }

        var resolution = await _mediator.Send(new ResolveExternalUserCommand("PiuGame",
                identity.GetLoginAliases().ToArray(), identity.GameTag, identity.GameTag, identity.ProfileImage),
            HttpContext.RequestAborted);

        await _currentUser.SetCurrentUser(resolution.User);

        var culture = await _uiSettings.GetSetting("Culture", HttpContext.RequestAborted, resolution.User.Id);
        if (culture != null)
            HttpContext.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(
                    new RequestCulture(culture, culture)));

        return LocalRedirect(resolution.IsNew ? "/Welcome" : string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
    }

    private static string AppendError(string url, string error)
    {
        return url + (url.Contains('?') ? "&" : "?") + $"error={error}";
    }

    [HttpGet("{providerName}/Link")]
    public async Task<IActionResult> LinkSignIn([FromRoute] string providerName)
    {
        if (!AllowedProviders.Contains(providerName)) return BadRequest("Invalid provider name");
        if (!_currentUser.IsLoggedIn) return LocalRedirect("/Login");

        if (!await HttpContext.IsProviderSupportedAsync(providerName)) return BadRequest();

        return Challenge(new AuthenticationProperties { RedirectUri = $"/Login/{providerName}/Link/Callback" },
            providerName);
    }

    [HttpGet("{providerName}/Link/Callback")]
    public async Task<IActionResult> LinkCallback([FromRoute] string providerName)
    {
        if (!AllowedProviders.Contains(providerName)) return BadRequest("Invalid provider name");
        if (!_currentUser.IsLoggedIn) return LocalRedirect("/Login");

        var authenticateResult = await HttpContext.AuthenticateAsync(providerName);

        if (authenticateResult.Principal == null) return BadRequest("Principal was missing");

        var id = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

        await HttpContext.SignOutAsync("ExternalAuthentication");

        var owner = await _mediator.Send(new GetUserByExternalLoginQuery(id, providerName),
            HttpContext.RequestAborted);

        string result;
        if (owner == null)
        {
            await _mediator.Send(new CreateExternalLoginCommand(_currentUser.User.Id, id, providerName),
                HttpContext.RequestAborted);
            result = "Linked";
        }
        else
        {
            result = owner.Id == _currentUser.User.Id ? "AlreadyLinked" : "Conflict";
        }

        return LocalRedirect($"/Account?linkResult={result}&linkProvider={providerName}");
    }

    // DevAuth backdoor (cherry-picked from the Phase-1 local-dev slice): environment-config
    // gated login without OAuth. Both endpoints 404 unless DevAuth:Enabled is true, which
    // only the local Aspire/dev configuration sets.
    [HttpPost("Dev")]
    public async Task<IActionResult> DevLogin([FromForm] Guid userId)
    {
        if (!_devAuth.Value.Enabled) return NotFound();

        var user = await _mediator.Send(new GetUserByIdQuery(userId), HttpContext.RequestAborted);
        if (user == null) return BadRequest("User not found");

        await _currentUser.SetCurrentUser(user);
        return LocalRedirect(await DevLandingPage());
    }

    [HttpPost("Dev/Bootstrap")]
    public async Task<IActionResult> DevLoginBootstrap()
    {
        if (!_devAuth.Value.Enabled) return NotFound();

        var user = await _mediator.Send(new CreateUserCommand("Dev User"), HttpContext.RequestAborted);
        await _currentUser.SetCurrentUser(user);
        return LocalRedirect(await DevLandingPage());
    }

    // An empty local catalog means setup isn't finished — land back on the setup page
    // instead of Welcome/home, which can't render anything useful yet.
    private async Task<string> DevLandingPage()
    {
        var hasCharts = (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix), HttpContext.RequestAborted)).Any();
        return hasCharts ? "/Welcome" : "/Dev/Populate";
    }
}

public static class HttpContextExtensions
{
    public static async Task<AuthenticationScheme[]> GetExternalProvidersAsync(this HttpContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var schemes = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();

        return (from scheme in await schemes.GetAllSchemesAsync()
            where !string.IsNullOrEmpty(scheme.DisplayName)
            select scheme).ToArray();
    }

    public static async Task<bool> IsProviderSupportedAsync(this HttpContext context, string provider)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        return (from scheme in await context.GetExternalProvidersAsync()
            where string.Equals(scheme.Name, provider, StringComparison.OrdinalIgnoreCase)
            select scheme).Any();
    }
}