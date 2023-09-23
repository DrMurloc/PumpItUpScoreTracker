using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface ICurrentUserAccessor
{
    bool IsLoggedIn { get; }
    User User { get; }
    Task SetCurrentUser(User user);
    bool IsLoggedInAsAdmin { get; }
}