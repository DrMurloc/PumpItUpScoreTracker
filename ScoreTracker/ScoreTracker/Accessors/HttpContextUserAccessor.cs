using System.Security.Claims;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Accessors;

public sealed class HttpContextUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _context;

    public HttpContextUserAccessor(IHttpContextAccessor httpContext)
    {
        _context = httpContext;
    }

    public Guid UserId =>
        Guid.Parse(_context.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
}