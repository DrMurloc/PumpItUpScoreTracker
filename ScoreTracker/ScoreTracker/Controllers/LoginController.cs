using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Controllers;

[Route("[controller]")]
public sealed class LoginController : Controller
{
    private static readonly ISet<string> AllowedProviders =
        new HashSet<string>(new[] { "Discord", "Google" }, StringComparer.OrdinalIgnoreCase);

    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public LoginController(IMediator mediator,
        ICurrentUserAccessor currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
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


        var user = await GetUserForExternalLogin(authenticateResult.Principal, providerName);

        await _currentUser.SetCurrentUser(user);

        return Redirect(returnUrl ?? "/");
    }

    private async Task<User> GetUserForExternalLogin(ClaimsPrincipal principal, string loginProviderName)
    {
        var id = principal.FindFirst(ClaimTypes.NameIdentifier)
            ?.Value ?? "";
        var name = principal.FindFirst(ClaimTypes.Name)?.Value ??
                   "Unknown Name";
        var user = await _mediator.Send(new GetUserByExternalLoginQuery(id, loginProviderName),
            HttpContext.RequestAborted);
        if (user != null) return user;

        user = await _mediator.Send(new CreateUserCommand(name), HttpContext.RequestAborted);
        await _mediator.Send(new CreateExternalLoginCommand(user.Id, id, loginProviderName),
            HttpContext.RequestAborted);

        return user;
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