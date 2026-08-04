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
    private static readonly IReadOnlySet<string> NoTags = new HashSet<string>();
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
    ///     The site-user rivals you can highlight. A board-only rival has no account, so no row on
    ///     a LIVE board is ever theirs — on the official boards, where they do appear, match
    ///     <see cref="GetMyRivalTags" /> as well.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> GetMyRivalIds(MixEnum mix)
    {
        await LoadRivals(mix);
        return _rivals!;
    }

    /// <summary>
    ///     The board tags you rival, for the official boards — the one place a rival with no site
    ///     account is a row rather than an absence. Matching those by user id can never hit: a
    ///     ghost has no account, and an official row for a linked player may not carry one either.
    /// </summary>
    public async Task<IReadOnlySet<string>> GetMyRivalTags(MixEnum mix)
    {
        await LoadRivals(mix);
        return _rivalTags!;
    }

    private async Task LoadRivals(MixEnum mix)
    {
        if (_rivals != null) return;
        if (!currentUser.IsLoggedIn)
        {
            _rivals = None;
            _rivalTags = NoTags;
            return;
        }

        var rivals = await mediator.Send(new GetMyRivalsQuery(mix));
        _rivals = rivals.Where(r => r.UserId != null).Select(r => r.UserId!.Value).ToHashSet();
        _rivalTags = rivals.Where(r => r.Tag != null).Select(r => r.Tag!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     The row class for one player, applying the precedence ladder: you → both → rival →
    ///     community. A rival who is also a clubmate is more interesting as both than as either.
    ///     <para>
    ///         Every board calls this rather than writing the ladder again. Each row family had
    ///         its own copy of the you/clubmate ternary, and the copies drifted: the ones written
    ///         before rivals existed never grew the rival arms, so a rival lit up on some boards
    ///         and not others. <paramref name="youClass" /> and <paramref name="communityClass" />
    ///         are the caller's native class names for those two states — the rival states use the
    ///         layout-agnostic utility set, which is the point of that set.
    ///     </para>
    ///     <para>Null sets are treated as empty, so a board can adopt this before it has both.</para>
    /// </summary>
    public static string RowClass(Guid userId, Guid? me, IReadOnlySet<Guid>? rivals,
        IReadOnlySet<Guid>? clubmates, string youClass, string communityClass = "is-community") =>
        RowClass(userId == me, rivals?.Contains(userId) == true, clubmates?.Contains(userId) == true,
            youClass, communityClass);

    /// <summary>
    ///     The same ladder for a board that already resolved the three memberships — it row-models
    ///     them as flags rather than re-testing sets per row. Both overloads exist so neither call
    ///     shape has an excuse to write the ladder again.
    /// </summary>
    public static string RowClass(bool isMe, bool isRival, bool isClubmate, string youClass,
        string communityClass = "is-community")
    {
        if (isMe) return youClass;
        return (isRival, isClubmate) switch
        {
            (true, true) => "is-both",
            (true, false) => "is-rival",
            (false, true) => communityClass,
            _ => string.Empty
        };
    }

    private IReadOnlySet<Guid>? _rivals;
    private IReadOnlySet<string>? _rivalTags;
}
