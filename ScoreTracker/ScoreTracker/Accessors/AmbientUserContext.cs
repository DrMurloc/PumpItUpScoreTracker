using ScoreTracker.Domain.Models;

namespace ScoreTracker.Web.Accessors;

// Carries the current user for a scope that has no HttpContext — a background bus consumer. The
// consumer sets it (through ICurrentUserAccessor.SetCurrentUser); HttpContextUserAccessor falls
// back to it when no HttpContext is authenticated. Scoped, so it never leaks across scopes.
public sealed class AmbientUserContext
{
    public User? User { get; set; }
}
