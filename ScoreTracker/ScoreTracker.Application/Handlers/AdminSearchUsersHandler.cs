using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class AdminSearchUsersHandler : IRequestHandler<AdminSearchUsersQuery, IEnumerable<User>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;

    public AdminSearchUsersHandler(IUserRepository users, ICurrentUserAccessor currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<User>> Handle(AdminSearchUsersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.User.IsAdmin)
            throw new NotAuthorizedException("search all users");

        var results = new Dictionary<Guid, User>();

        if (Guid.TryParse(request.SearchText, out var guidId))
        {
            var byId = await _users.GetUser(guidId, cancellationToken);
            if (byId != null) results[byId.Id] = byId;
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var byName = await _users.SearchForUsersByName(request.SearchText, cancellationToken);
            foreach (var user in byName) results[user.Id] = user;
        }

        return results.Values.OrderBy(u => u.Name.ToString()).ToArray();
    }
}
