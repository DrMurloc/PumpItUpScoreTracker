using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;

namespace ScoreTracker.Domain.Services;

public sealed class UserAccessService : IUserAccessService
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public UserAccessService(ICurrentUserAccessor currentUser,
        IUserRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<bool> HasAccessTo(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsLoggedIn && _currentUser.User.Id == targetUserId) return true;

        var user = await _users.GetUser(targetUserId, cancellationToken);

        return user is { IsPublic: true };
    }
}