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
        : new User(
            Guid.Parse(_context.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString()),
            _context.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value ?? "Unauthenticated",
            bool.Parse(_context.HttpContext?.User.FindFirstValue(ScoreTrackerClaimTypes.IsPublic) ?? ""));

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
}