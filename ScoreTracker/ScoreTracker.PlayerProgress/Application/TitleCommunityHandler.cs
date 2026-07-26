using MediatR;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     The community side of titles: how rare each one is, and who holds a given one. Reads
///     only — the titles page used to reach for the repositories itself,
///     which is what these two queries exist to stop.
/// </summary>
internal sealed class TitleCommunityHandler :
    IRequestHandler<GetTitleRarityQuery, TitleRarityRecord>,
    IRequestHandler<GetTitleHoldersQuery, TitleHoldersRecord>
{
    private readonly ITitleRepository _titles;
    private readonly IUserReader _users;

    public TitleCommunityHandler(ITitleRepository titles, IUserReader users)
    {
        _titles = titles;
        _users = users;
    }

    public async Task<TitleRarityRecord> Handle(GetTitleRarityQuery request, CancellationToken cancellationToken)
    {
        var aggregations = await _titles.GetTitleAggregations(request.Mix, cancellationToken);
        var trackedPlayers = await _titles.CountTitledUsers(cancellationToken);
        return new TitleRarityRecord(
            aggregations.ToDictionary(a => a.Title, a => a.Count),
            trackedPlayers);
    }

    public async Task<TitleHoldersRecord> Handle(GetTitleHoldersQuery request, CancellationToken cancellationToken)
    {
        var achieved = (await _titles.GetUsersWithTitle(request.Mix, request.Title, cancellationToken)).ToArray();
        if (achieved.Length == 0) return new TitleHoldersRecord(Array.Empty<TitleHolder>(), 0);

        var users = (await _users.GetUsers(achieved.Select(a => a.UserId), cancellationToken))
            .ToDictionary(u => u.Id);

        // A private profile stays out of the list entirely; the drawer only learns how many.
        var holders = achieved
            .Where(a => users.TryGetValue(a.UserId, out var user) && user.IsPublic)
            .Select(a => new TitleHolder(users[a.UserId]))
            .OrderBy(h => h.User.Name.ToString())
            .ToArray();

        return new TitleHoldersRecord(holders, achieved.Length - holders.Length);
    }
}
