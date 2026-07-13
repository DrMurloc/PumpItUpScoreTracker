using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Accessors;

public sealed class HttpContextUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _context;
    private readonly AmbientUserContext _ambient;

    public HttpContextUserAccessor(IHttpContextAccessor httpContext, AmbientUserContext ambient)
    {
        _context = httpContext;
        _ambient = ambient;
    }

    private bool HttpAuthenticated => _context?.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    // An authenticated HttpContext wins; otherwise fall back to the user a background job set.
    public bool IsLoggedIn => HttpAuthenticated || _ambient.User != null;

    public User User => HttpAuthenticated
        ? _context.HttpContext!.User.GetUser()
        : _ambient.User ?? throw new UserNotLoggedInException();

    public async Task SetCurrentUser(User user)
    {
        // Set the ambient user, then issue the sign-in cookie. Reserved for real HTTP requests —
        // a background job must use SetScopedUser so it never signs the flowed circuit out.
        _ambient.User = user;
        var context = _context.HttpContext;
        if (context == null) return;
        var principal = user.GetClaimsPrincipal();
        await context.SignOutAsync();
        await context.SignInAsync(principal);
    }

    public void SetScopedUser(User user)
    {
        _ambient.User = user;
    }

    public bool IsLoggedInAsAdmin => IsLoggedIn && User.IsAdmin;
}

public static class UserExtensions
{
    public static ClaimsPrincipal GetClaimsPrincipal(this User user)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, "User"),
            new Claim(ScoreTrackerClaimTypes.IsPublic, user.IsPublic.ToString()),
            new Claim(ScoreTrackerClaimTypes.ProfileImage, user.ProfileImage.ToString()),
            new Claim(ScoreTrackerClaimTypes.GameTag, user.GameTag?.ToString() ?? ""),
            new Claim(ScoreTrackerClaimTypes.Country, user.Country?.ToString() ?? ""),
            new Claim(ScoreTrackerClaimTypes.IsContentLocked, user.IsContentLocked.ToString()),
            new Claim(ScoreTrackerClaimTypes.ClaimsIssuedAt, DateTimeOffset.UtcNow.ToString("O"))
        }, "External"));
    }

    public static User GetUser(this ClaimsPrincipal claimsPrincipal)
    {
        return new User(
            Guid.Parse(claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString()),
            claimsPrincipal.FindFirst(ClaimTypes.Name)?.Value ?? "Unauthenticated",
            bool.Parse(claimsPrincipal.FindFirstValue(ScoreTrackerClaimTypes.IsPublic) ?? ""),
            string.IsNullOrWhiteSpace(claimsPrincipal.FindFirstValue(ScoreTrackerClaimTypes.GameTag))
                ? null
                : claimsPrincipal.FindFirstValue(ScoreTrackerClaimTypes.GameTag),
            Uri.TryCreate(claimsPrincipal.FindFirstValue(ScoreTrackerClaimTypes.ProfileImage) ?? "", UriKind.Absolute,
                out var imagePath)
                ? imagePath
                : new Uri("https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png",
                    UriKind.Absolute),
            string.IsNullOrWhiteSpace(claimsPrincipal.FindFirstValue(ScoreTrackerClaimTypes.Country))
                ? null
                : claimsPrincipal.FindFirstValue(ScoreTrackerClaimTypes.Country),
            bool.TryParse(claimsPrincipal.FindFirstValue(ScoreTrackerClaimTypes.IsContentLocked), out var locked)
            && locked);
    }
}