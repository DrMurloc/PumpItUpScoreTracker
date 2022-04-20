using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Web.Controllers;

[Route("[controller]")]
public sealed class LoginController : Controller
{
    private readonly IMediator _mediator;

    public LoginController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{providerName}")]
    public async Task<IActionResult> SignIn([FromRoute] string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName)) return BadRequest();

        if (!await HttpContext.IsProviderSupportedAsync(providerName)) return BadRequest();

        return Challenge(new AuthenticationProperties { RedirectUri = $"/Login/{providerName}/Callback" },
            providerName);
    }

    [HttpGet("{providerName}/Callback")]
    public async Task<IActionResult> ExternalCallback([FromRoute] string providerName)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(providerName);
        if (authenticateResult.Principal == null) return BadRequest("Principal was missing");

        var returnUrl = "";
        authenticateResult.Ticket?.Properties.Items.TryGetValue("returnUrl", out returnUrl);

        var user = await GetUserForDiscordLogin(authenticateResult.Principal);


        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name)
        }));
        await HttpContext.SignOutAsync();
        await HttpContext.SignInAsync(principal);
        return Redirect(returnUrl ?? "/");
    }

    private async Task<User> GetUserForDiscordLogin(ClaimsPrincipal principal)
    {
        var id = ulong.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)
            ?.Value ?? "");
        var name = principal.FindFirst(ClaimTypes.Name)?.Value ??
                   "Unknown Name";
        var user = await _mediator.Send(new GetUserByDiscordLoginQuery(id), HttpContext.RequestAborted);
        if (user != null) return user;

        user = await _mediator.Send(new CreateUserCommand(name), HttpContext.RequestAborted);
        await _mediator.Send(new CreateDiscordLoginCommand(user.Id, id), HttpContext.RequestAborted);

        return user;
    }

    [HttpGet("signout")]
    [HttpPost("signout")]
    public IActionResult SignOutCurrentUser()
    {
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme);
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