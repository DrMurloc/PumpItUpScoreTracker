using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface ICurrentUserAccessor
{
    bool IsLoggedIn { get; }
    User User { get; }

    // Signs the user in for a real request (issues the auth cookie). Only call this on an HTTP
    // request — login/logout — never from a background job.
    Task SetCurrentUser(User user);

    // Establishes the current user for this DI scope only, without touching authentication. This
    // is what a background bus consumer uses so its inner handlers resolve the job's user — issuing
    // a cookie there would sign out the live circuit whose context happened to flow in.
    void SetScopedUser(User user);

    bool IsLoggedInAsAdmin { get; }
}