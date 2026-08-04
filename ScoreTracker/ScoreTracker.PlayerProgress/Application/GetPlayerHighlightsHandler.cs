using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     Both reads of the significant-win ledger (docs/design/rivals.md §2.4): by player for a
///     feed that fans in on its own audience, and by event for an audience index that already
///     picked its rows. Player name and avatar resolve fresh through the published user reader —
///     never a SQL join onto Identity's tables — so a rename shows up on old rows too.
/// </summary>
internal sealed class GetPlayerHighlightsHandler
    : IRequestHandler<GetPlayerHighlightsQuery, IEnumerable<PlayerHighlightRecord>>,
        IRequestHandler<GetPlayerHighlightsForEventsQuery, IEnumerable<PlayerHighlightRecord>>
{
    private readonly IPlayerHighlightRepository _highlights;
    private readonly IUserReader _users;

    public GetPlayerHighlightsHandler(IPlayerHighlightRepository highlights, IUserReader users)
    {
        _highlights = highlights;
        _users = users;
    }

    public async Task<IEnumerable<PlayerHighlightRecord>> Handle(GetPlayerHighlightsQuery request,
        CancellationToken cancellationToken)
    {
        return await Resolve(
            await _highlights.GetForUsers(request.UserIds, request.Mix, request.Take, cancellationToken),
            cancellationToken);
    }

    public async Task<IEnumerable<PlayerHighlightRecord>> Handle(GetPlayerHighlightsForEventsQuery request,
        CancellationToken cancellationToken)
    {
        return await Resolve(
            await _highlights.GetForEvents(request.EventIds, cancellationToken),
            cancellationToken);
    }

    private async Task<IEnumerable<PlayerHighlightRecord>> Resolve(IReadOnlyList<PlayerHighlightEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0) return Array.Empty<PlayerHighlightRecord>();

        var users = (await _users.GetUsers(entries.Select(e => e.UserId).Distinct(), cancellationToken))
            .ToDictionary(u => u.Id);

        // A winner the user reader cannot resolve has been deleted between the write and this
        // read. Dropping the row is the only honest option — there is nobody left to name.
        return entries
            .Where(e => users.ContainsKey(e.UserId))
            .Select(e =>
            {
                var user = users[e.UserId];
                return new PlayerHighlightRecord(e.EventId, e.UserId, user.Name.ToString(), user.ProfileImage,
                    user.IsPublic, e.Mix, e.OccurredAt, e.SessionId, e.Wins);
            })
            .ToArray();
    }
}
