using System.Security.Claims;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;
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
    private readonly IUserRepository _users;
    private readonly IOptions<DevAuthConfiguration> _devAuth;

    public LoginController(IMediator mediator,
        ICurrentUserAccessor currentUser,
        IUiSettingsAccessor uiSettings,
        IUserRepository users,
        IOptions<DevAuthConfiguration> devAuth)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _uiSettings = uiSettings;
        _users = users;
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

    [HttpPost("Dev")]
    public async Task<IActionResult> DevLogin([FromForm] Guid userId)
    {
        if (!_devAuth.Value.Enabled) return NotFound();

        var user = await _users.GetUser(userId, HttpContext.RequestAborted);
        if (user == null) return BadRequest("User not found");

        await _currentUser.SetCurrentUser(user);
        return LocalRedirect("/");
    }

    [HttpPost("Dev/Bootstrap")]
    public async Task<IActionResult> DevLoginBootstrap()
    {
        if (!_devAuth.Value.Enabled) return NotFound();

        var user = await _mediator.Send(new CreateUserCommand("Dev User"), HttpContext.RequestAborted);
        await _currentUser.SetCurrentUser(user);
        return LocalRedirect("/Welcome");
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