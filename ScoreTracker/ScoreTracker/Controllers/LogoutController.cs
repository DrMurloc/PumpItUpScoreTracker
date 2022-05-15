using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Controllers;

[Route("[controller]")]
public sealed class LogoutController : Controller
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public LogoutController(IMediator mediator,
        ICurrentUserAccessor currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("Refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!_currentUser.IsLoggedIn) return BadRequest("Not logged in");
        var user = await _mediator.Send(new GetUserByIdQuery(_currentUser.User.Id));

        if (user == null) return BadRequest("User was not found");

        await _currentUser.SetCurrentUser(user);
        return Ok();
    }

    [HttpGet("")]
    public async Task<IActionResult> LogOut()
    {
        await HttpContext.SignOutAsync();
        return Redirect("/Login");
    }
}