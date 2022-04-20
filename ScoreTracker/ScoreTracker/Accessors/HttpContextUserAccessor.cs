using System.Security.Claims;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

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
            _context.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value ?? "Unauthenticated");
}