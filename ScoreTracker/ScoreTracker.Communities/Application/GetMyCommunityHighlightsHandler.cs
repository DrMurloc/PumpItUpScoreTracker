using MediatR;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Reads the community big-wins feed for the current user (docs/design/home-page-widgets.md §7).
///     The repository already gates on the caller's membership and dedupes per event; this layers on
///     the own-wins toggle and resolves player name/avatar fresh through the published user reader
///     (never a SQL join onto Identity's tables).
/// </summary>
internal sealed class GetMyCommunityHighlightsHandler
    : IRequestHandler<GetMyCommunityHighlightsQuery, IEnumerable<CommunityHighlightRecord>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ICommunityHighlightRepository _highlights;
    private readonly IUserReader _users;

    public GetMyCommunityHighlightsHandler(ICommunityHighlightRepository highlights,
        ICurrentUserAccessor currentUser, IUserReader users)
    {
        _highlights = highlights;
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<IEnumerable<CommunityHighlightRecord>> Handle(GetMyCommunityHighlightsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn || request.Communities.Count == 0)
            return Array.Empty<CommunityHighlightRecord>();

        var userId = _currentUser.User.Id;
        var entries = await _highlights.GetForUser(userId, request.Communities, request.Mix, request.Take,
            cancellationToken);
        var visible = request.IncludeOwnWins ? entries : entries.Where(e => e.UserId != userId).ToArray();
        if (visible.Count == 0) return Array.Empty<CommunityHighlightRecord>();

        var users = (await _users.GetUsers(visible.Select(e => e.UserId).Distinct(), cancellationToken))
            .ToDictionary(u => u.Id);

        return visible
            .Where(e => users.ContainsKey(e.UserId))
            .Select(e =>
            {
                var user = users[e.UserId];
                return new CommunityHighlightRecord(e.UserId, user.Name.ToString(), user.ProfileImage,
                    user.IsPublic, e.Mix, e.OccurredAt, e.SessionId, e.Wins);
            })
            .ToArray();
    }
}
