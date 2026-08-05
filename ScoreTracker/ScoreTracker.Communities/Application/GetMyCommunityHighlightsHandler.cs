using MediatR;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Reads the community big-wins feed for the current user (docs/design/home-page-widgets.md §7).
///     Two steps since the payload moved out (docs/design/rivals.md D32): the index answers WHICH
///     events this member may see, then PlayerProgress answers what they were. Membership is still
///     the consent gate and it is still applied here, in the index read.
/// </summary>
internal sealed class GetMyCommunityHighlightsHandler
    : IRequestHandler<GetMyCommunityHighlightsQuery, IEnumerable<PlayerHighlightRecord>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ICommunityHighlightRepository _index;
    private readonly IMediator _mediator;

    public GetMyCommunityHighlightsHandler(ICommunityHighlightRepository index,
        ICurrentUserAccessor currentUser, IMediator mediator)
    {
        _index = index;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<IEnumerable<PlayerHighlightRecord>> Handle(GetMyCommunityHighlightsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn || request.Communities.Count == 0)
            return Array.Empty<PlayerHighlightRecord>();

        var userId = _currentUser.User.Id;
        var eventIds = await _index.GetVisibleEventIds(userId, request.Communities, request.Mix, request.Take,
            cancellationToken);
        if (eventIds.Count == 0) return Array.Empty<PlayerHighlightRecord>();

        var records = (await _mediator.Send(new GetPlayerHighlightsForEventsQuery(eventIds), cancellationToken))
            .ToDictionary(r => r.EventId);

        // The index already produced the order it wants; re-sorting on OccurredAt here would
        // throw that away. Events whose payload has aged out of the ledger simply drop.
        return eventIds
            .Where(records.ContainsKey)
            .Select(id => records[id])
            .Where(r => request.IncludeOwnWins || r.UserId != userId)
            .ToArray();
    }
}
