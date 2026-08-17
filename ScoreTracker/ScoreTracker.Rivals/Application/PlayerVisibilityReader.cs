using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     Rivals' implementation of the published <see cref="IPlayerVisibilityReader" />: the two
///     non-public bases a viewer can hold on another player — a shared user-created community
///     and a rival edge — read once into a <see cref="PlayerAudience" />. This vertical hosts the
///     port because it is the one that can already see both sources; the port is what consumers
///     depend on, and the implementation moves with the peer abstraction when that exists
///     (docs/design/peers-abstraction.md §1).
///     <para>
///         USER-CREATED communities only. World and the country communities are joined
///         automatically, so counting them would make "people you know" mean everybody and the
///         private-account gate would protect nobody — the community read already leaves them out.
///     </para>
/// </summary>
internal sealed class PlayerVisibilityReader : IPlayerVisibilityReader
{
    private readonly ICommunityReader _communities;
    private readonly IRivalRepository _rivals;

    public PlayerVisibilityReader(ICommunityReader communities, IRivalRepository rivals)
    {
        _communities = communities;
        _rivals = rivals;
    }

    public async Task<PlayerAudience> GetAudience(Guid? viewerId, CancellationToken cancellationToken = default)
    {
        if (viewerId is not { } viewer) return PlayerAudience.Anonymous;

        var byCommunity = await _communities.GetUserCommunityMembers(viewer, cancellationToken);
        var byMember = new Dictionary<Guid, List<Name>>();
        foreach (var (community, members) in byCommunity)
        foreach (var member in members)
        {
            // The viewer's own seat is not a community they share with themselves.
            if (member == viewer) continue;
            if (!byMember.TryGetValue(member, out var names)) byMember[member] = names = new List<Name>();
            names.Add(community);
        }

        var rivalTargets = (await _rivals.GetRivalsOwnedBy(viewer, cancellationToken))
            .Where(e => e.TargetUserId != null)
            .Select(e => e.TargetUserId!.Value)
            .ToHashSet();

        return new PlayerAudience(viewer,
            byMember.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<Name>)kv.Value.OrderBy(n => n).ToArray()),
            rivalTargets);
    }
}
