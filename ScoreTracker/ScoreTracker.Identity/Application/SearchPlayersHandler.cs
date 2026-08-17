using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.Identity.Application;

/// <summary>
///     Identity searches players; the visibility port says which. Identity learns nothing about
///     what a community or a rival is — it asks the port for the ids the caller may see, hands
///     them to the repository as the private-player allowance, and stamps each hit with the basis
///     the port reports so the row can say why it is there.
/// </summary>
internal sealed class SearchPlayersHandler : IRequestHandler<SearchPlayersQuery, IReadOnlyList<PlayerSearchHit>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;
    private readonly IPlayerVisibilityReader _visibility;

    public SearchPlayersHandler(IUserRepository users, IPlayerVisibilityReader visibility,
        ICurrentUserAccessor currentUser)
    {
        _users = users;
        _visibility = visibility;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PlayerSearchHit>> Handle(SearchPlayersQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Term) || request.Take <= 0) return Array.Empty<PlayerSearchHit>();

        var viewer = _currentUser.IsLoggedIn ? _currentUser.User.Id : (Guid?)null;
        var audience = await _visibility.GetAudience(viewer, cancellationToken);
        var users = await _users.SearchVisibleUsers(request.Term.Trim(), request.Take, audience.VisibleUserIds,
            cancellationToken);

        return users
            .Select(u => new PlayerSearchHit(u.Id, u.Name, u.GameTag, u.ProfileImage, u.Country,
                audience.Describe(u.Id, u.IsPublic)))
            .ToArray();
    }
}
