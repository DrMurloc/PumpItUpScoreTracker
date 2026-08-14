using MediatR;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

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
    // The eight total-pool [P.B] rungs are the only titles whose holders subdivide by level.
    // Resolved from the shipped taxonomy rather than spelled out, so a renamed gem cannot leave
    // a stale name behind here.
    private static readonly IReadOnlySet<Name> TotalPumbilityGems = Phoenix2TitleList.BuildList()
        .OfType<Phoenix2PumbilityTitle>()
        .Where(t => t.Pool == PumbilityPool.Total)
        .Select(t => t.Name)
        .ToHashSet();

    private readonly IPlayerStatsReader _stats;
    private readonly ITitleRepository _titles;
    private readonly IUserReader _users;

    public TitleCommunityHandler(ITitleRepository titles, IUserReader users, IPlayerStatsReader stats)
    {
        _titles = titles;
        _users = users;
        _stats = stats;
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
            .ToArray();

        holders = await AttachPools(request, holders, cancellationToken);

        // Pool-less titles all sort by name (every pool is null); the gem rungs read strongest
        // first, which is the order their level groups render in.
        return new TitleHoldersRecord(
            holders
                .OrderByDescending(h => h.TotalPumbility ?? double.MinValue)
                .ThenBy(h => h.User.Name.ToString())
                .ToArray(),
            standing.Length - holders.Length, climbedPast);
    }

    /// <summary>
    ///     On the eight total-PUMBILITY gems the drawer subdivides holders by level, and the level
    ///     is a function of the pool — so those rungs, and only those, pay one extra batch read.
    ///     The pool rides the record raw; presentation is the only thing that rounds one.
    /// </summary>
    private async Task<TitleHolder[]> AttachPools(GetTitleHoldersQuery request, TitleHolder[] holders,
        CancellationToken cancellationToken)
    {
        if (request.Mix != MixEnum.Phoenix2 || holders.Length == 0 ||
            !TotalPumbilityGems.Contains(request.Title))
            return holders;

        var pools = (await _stats.GetStats(request.Mix, holders.Select(h => h.User.Id), cancellationToken))
            .ToDictionary(s => s.UserId, s => s.SkillRating);
        return holders
            .Select(h => pools.TryGetValue(h.User.Id, out var pool) ? h with { TotalPumbility = pool } : h)
            .ToArray();
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
