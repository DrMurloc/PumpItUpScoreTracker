using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Rivals.Domain;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     The three roster-shaped reads: who you rival, who rivals you, and who you've blocked.
/// </summary>
internal sealed class GetMyRivalsHandler :
    IRequestHandler<GetMyRivalsQuery, IReadOnlyList<RivalSubject>>,
    IRequestHandler<GetRivalsOfMeQuery, IReadOnlyList<RivalOfMeRecord>>,
    IRequestHandler<GetMyBlockedPlayersQuery, IReadOnlyList<BlockedPlayerRecord>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly RivalSubjectResolver _resolver;
    private readonly IRivalRepository _rivals;
    private readonly IUserReader _users;
    private readonly IPlayerVisibilityReader _visibility;

    public GetMyRivalsHandler(IRivalRepository rivals, RivalSubjectResolver resolver, IUserReader users,
        IPlayerVisibilityReader visibility, ICurrentUserAccessor currentUser)
    {
        _rivals = rivals;
        _resolver = resolver;
        _users = users;
        _visibility = visibility;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<RivalSubject>> Handle(GetMyRivalsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return Array.Empty<RivalSubject>();
        var edges = await _rivals.GetRivalsOwnedBy(_currentUser.User.Id, cancellationToken);
        return await _resolver.Resolve(edges, request.Mix, cancellationToken);
    }

    /// <summary>
    ///     Everyone, including private accounts. This list is the only revocation the system has,
    ///     so hiding a row would leave somebody watching you with no way to stop them
    ///     (docs/design/rivals.md D13/D14).
    /// </summary>
    public async Task<IReadOnlyList<RivalOfMeRecord>> Handle(GetRivalsOfMeQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return Array.Empty<RivalOfMeRecord>();
        var me = _currentUser.User.Id;

        var inbound = await _rivals.GetRivalsTargeting(me, cancellationToken);
        if (inbound.Count == 0) return Array.Empty<RivalOfMeRecord>();

        var users = (await _users.GetUsers(inbound.Select(e => e.OwnerUserId).Distinct(), cancellationToken))
            .ToDictionary(u => u.Id);
        var clubmates = (await _visibility.GetAudience(me, cancellationToken)).SharedCommunitiesByMember;
        var mine = (await _rivals.GetRivalsOwnedBy(me, cancellationToken))
            .Where(e => e.TargetUserId != null)
            .Select(e => e.TargetUserId!.Value)
            .ToHashSet();

        return inbound
            .Where(e => users.ContainsKey(e.OwnerUserId))
            .Select(e =>
            {
                var user = users[e.OwnerUserId];
                return new RivalOfMeRecord(e.Id, e.OwnerUserId, user.Name.ToString(), user.ProfileImage,
                    user.IsPublic, clubmates.ContainsKey(e.OwnerUserId), mine.Contains(e.OwnerUserId), e.AddedAt);
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<BlockedPlayerRecord>> Handle(GetMyBlockedPlayersQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return Array.Empty<BlockedPlayerRecord>();

        var blocks = await _rivals.GetBlockedBy(_currentUser.User.Id, cancellationToken);
        if (blocks.Count == 0) return Array.Empty<BlockedPlayerRecord>();

        var users = (await _users.GetUsers(blocks.Select(b => b.BlockedUserId), cancellationToken))
            .ToDictionary(u => u.Id);

        return blocks
            .Where(b => users.ContainsKey(b.BlockedUserId))
            .Select(b => new BlockedPlayerRecord(b.BlockedUserId, users[b.BlockedUserId].Name.ToString(),
                users[b.BlockedUserId].ProfileImage, b.CreatedAt))
            .ToArray();
    }
}
