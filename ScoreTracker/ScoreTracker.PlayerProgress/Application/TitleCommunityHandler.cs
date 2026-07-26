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
        var (standing, climbedPast) = await WhoIsStandingHere(request, cancellationToken);
        if (standing.Length == 0) return new TitleHoldersRecord(Array.Empty<TitleHolder>(), 0, climbedPast);

        var users = (await _users.GetUsers(standing, cancellationToken)).ToDictionary(u => u.Id);

        // A private profile stays out of the list entirely; the drawer only learns how many.
        var holders = standing
            .Where(id => users.TryGetValue(id, out var user) && user.IsPublic)
            .Select(id => new TitleHolder(users[id]))
            .OrderBy(h => h.User.Name.ToString())
            .ToArray();

        return new TitleHoldersRecord(holders, standing.Length - holders.Length, climbedPast);
    }

    /// <summary>
    ///     Splits a rung's holders into the ones standing on it and the ones who have climbed
    ///     past. Off a ladder every holder is standing on it, and one read answers that. On a
    ///     ladder it takes the whole rail in one read instead — the rungs above this one are
    ///     what say whether a holder has moved on, and asking per rung would be N queries.
    /// </summary>
    private async Task<(Guid[] Standing, int ClimbedPast)> WhoIsStandingHere(GetTitleHoldersQuery request,
        CancellationToken cancellationToken)
    {
        var above = request.Ladder.SkipWhile(t => t != request.Title).Skip(1).ToArray();
        if (above.Length == 0)
        {
            var holders = await _titles.GetUsersWithTitle(request.Mix, request.Title, cancellationToken);
            return (holders.Select(h => h.UserId).Distinct().ToArray(), 0);
        }

        var rail = (await _titles.GetUsersWithTitles(request.Mix, above.Append(request.Title), cancellationToken))
            .ToArray();
        var higher = above.ToHashSet();
        var climbers = rail.Where(r => higher.Contains(r.Title)).Select(r => r.UserId).ToHashSet();
        var standing = rail.Where(r => r.Title == request.Title && !climbers.Contains(r.UserId))
            .Select(r => r.UserId).Distinct().ToArray();

        return (standing, climbers.Count);
    }
}
