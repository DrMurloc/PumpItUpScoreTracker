using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.Identity.Application;

/// <summary>
///     The visibility bases for a set of players by id — one audience read, then the same pure
///     question per player that the search answers for its hits. Anonymous callers see public
///     players and nothing about anyone else.
/// </summary>
internal sealed class GetPlayersVisibilityHandler
    : IRequestHandler<GetPlayersVisibilityQuery, IReadOnlyDictionary<Guid, PlayerVisibility>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserRepository _users;
    private readonly IPlayerVisibilityReader _visibility;

    public GetPlayersVisibilityHandler(IUserRepository users, IPlayerVisibilityReader visibility,
        ICurrentUserAccessor currentUser)
    {
        _users = users;
        _visibility = visibility;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyDictionary<Guid, PlayerVisibility>> Handle(GetPlayersVisibilityQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserIds.Count == 0) return new Dictionary<Guid, PlayerVisibility>();

        var viewer = _currentUser.IsLoggedIn ? _currentUser.User.Id : (Guid?)null;
        var audience = await _visibility.GetAudience(viewer, cancellationToken);
        var users = await _users.GetUsers(request.UserIds.Distinct(), cancellationToken);
        return users.ToDictionary(u => u.Id, u => audience.Describe(u.Id, u.IsPublic));
    }
}
