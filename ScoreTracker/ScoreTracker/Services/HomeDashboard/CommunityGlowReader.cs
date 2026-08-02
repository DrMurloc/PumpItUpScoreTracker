using MediatR;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     Who to highlight on a leaderboard row: the players who share a community with you (green,
///     opt-in crews only so the automatic region and world communities drop out) and the players
///     you rival (red). Circuit-scoped and memoized so several widgets on one page share one
///     lookup of each.
///     <para>
///         One service rather than two because every consumer needs both sets to decide a single
///         row class — a row that is both gets the segmented treatment, and that can only be
///         decided with both answers in hand (docs/design/rivals.md D40).
///     </para>
/// </summary>
public sealed class CommunityGlowReader(IMediator mediator, ICurrentUserAccessor currentUser)
{
    private static readonly IReadOnlySet<Guid> None = new HashSet<Guid>();
    private IReadOnlySet<Guid>? _cached;

    public async Task<IReadOnlySet<Guid>> GetMyCommunityMemberIds()
    {
        if (_cached != null) return _cached;
        if (!currentUser.IsLoggedIn) return _cached = None;

        var mine = (await mediator.Send(new GetMyCommunitiesQuery()))
            .Where(c => !c.IsRegional && c.CommunityName.ToString() != "World")
            .ToArray();
        if (mine.Length == 0) return _cached = None;

        var ids = new HashSet<Guid>();
        foreach (var community in mine)
        foreach (var id in await mediator.Send(new GetCommunityMembersQuery(community.CommunityName)))
            ids.Add(id);
        ids.Remove(currentUser.User.Id); // you glow blue, not green
        return _cached = ids;
    }

    /// <summary>
    ///     The site-user rivals you can highlight. Board-only rivals are absent by construction —
    ///     they have no account, so no row on a live board is theirs.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> GetMyRivalIds(MixEnum mix)
    {
        if (_rivals != null) return _rivals;
        if (!currentUser.IsLoggedIn) return _rivals = None;

        var rivals = await mediator.Send(new GetMyRivalsQuery(mix));
        return _rivals = rivals.Where(r => r.UserId != null).Select(r => r.UserId!.Value).ToHashSet();
    }

    /// <summary>
    ///     The row class for one player, applying the precedence ladder: you → both → rival →
    ///     community. A rival who is also a clubmate is more interesting as both than as either.
    /// </summary>
    public static string RowClass(Guid userId, Guid? me, IReadOnlySet<Guid> rivals,
        IReadOnlySet<Guid> clubmates, string youClass)
    {
        if (userId == me) return youClass;
        var isRival = rivals.Contains(userId);
        var isClubmate = clubmates.Contains(userId);
        return (isRival, isClubmate) switch
        {
            (true, true) => "is-both",
            (true, false) => "is-rival",
            (false, true) => "is-community",
            _ => string.Empty
        };
    }

    private IReadOnlySet<Guid>? _rivals;
}
