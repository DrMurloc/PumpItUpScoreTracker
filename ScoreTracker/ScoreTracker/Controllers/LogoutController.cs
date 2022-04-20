using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace ScoreTracker.Web.Controllers;

[Route("[controller]")]
public sealed class LogoutController : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> LogOut()
    {
        await HttpContext.SignOutAsync();
        return Redirect("/Login");
    }
}