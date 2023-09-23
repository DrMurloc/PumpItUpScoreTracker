using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Accessors;

public sealed class HttpContextUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _context;

    public HttpContextUserAccessor(IHttpContextAccessor httpContext)
    {
        _context = httpContext;
    }

    public bool IsLoggedIn => _context?.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public User User => !IsLoggedIn
        ? throw new UserNotLoggedInException()
        : _context.HttpContext?.User.GetUser() ?? throw new UserNotLoggedInException();


    public async Task SetCurrentUser(User user)
    {
        var context = _context.HttpContext;
        if (context == null) return;
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, "User"),
            new Claim(ScoreTrackerClaimTypes.IsPublic, user.IsPublic.ToString())
        }, "External"));
        await context.SignOutAsync();
        await context.SignInAsync(principal);
    }

    public bool IsLoggedInAsAdmin => IsLoggedIn && User.IsAdmin;
}

public static class UserExtensions
{
    public static User GetUser(this ClaimsPrincipal claimsPrincipal)
    {
        return new User(
            Guid.Parse(claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString()),
            claimsPrincipal.FindFirst(ClaimTypes.Name)?.Value ?? "Unauthenticated",
            bool.Parse(claimsPrincipal.FindFirstValue(ScoreTrackerClaimTypes.IsPublic) ?? ""));
    }
}