using MediatR;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Web.Services.HomeDashboard;

/// <summary>
///     The players who share a community with you — the opt-in crews only, so the automatic region
///     and world communities drop out. Powers the green "your people" highlight on dashboard
///     leaderboards. Circuit-scoped and memoized so several widgets on one page share one lookup.
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
}
