using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Controllers;

[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class LoginController : Controller
{
    private static readonly ISet<string> AllowedProviders =
        new HashSet<string>(new[] { "Discord", "Google", "Facebook" }, StringComparer.OrdinalIgnoreCase);

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

        var url = isNewUser ? "/Welcome" : returnUrl;

        return Redirect(url ?? "/");
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