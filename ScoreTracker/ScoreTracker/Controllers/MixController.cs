using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services;

namespace ScoreTracker.Web.Controllers;

[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class MixController : Controller
{
    private const string MixSettingKey = "Universal__CurrentMix";
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(30);

    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public MixController(IMediator mediator, ICurrentUserAccessor currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    ///     Switches the mix and reloads where it was called from. The shell reads the selection
    ///     server-side, so changing it is a real navigation rather than a circuit round-trip —
    ///     which is what it already was, since switching mixes always forced a reload.
    /// </summary>
    [HttpGet("Set")]
    public async Task<IActionResult> Set([FromQuery(Name = "mix")] string mix,
        [FromQuery(Name = "redirectUrl")] string redirectUrl)
    {
        if (Enum.TryParse<MixEnum>(mix, out var parsed))
        {
            if (_currentUser.IsLoggedIn)
                await _mediator.Send(new SaveUserUiSettingCommand(MixSettingKey, parsed.ToString()));

            // Written for signed-in users too: it keeps the selection intact through logout, and
            // it is what a cache key can be built from without knowing who is asking.
            Response.Cookies.Append(ShellModelFactory.MixCookieName, parsed.ToString(), new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.Add(CookieLifetime),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });
        }

        return LocalRedirect(redirectUrl);
    }
}
