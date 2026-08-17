using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models;

/// <summary>
///     Everyone a viewer may look at beyond the public players: themselves, the members of the
///     user-created communities they belong to (each with the community names shared), and the
///     site players they hold a rival edge onto. Read once through
///     <see cref="SecondaryPorts.IPlayerVisibilityReader" />, then answers per-player questions
///     without another round trip. A viewer of <c>null</c> is anonymous and sees public players
///     only.
/// </summary>
public sealed record PlayerAudience(
    Guid? ViewerId,
    IReadOnlyDictionary<Guid, IReadOnlyList<Name>> SharedCommunitiesByMember,
    IReadOnlySet<Guid> RivalTargetIds)
{
    public static PlayerAudience Anonymous { get; } = new(null,
        new Dictionary<Guid, IReadOnlyList<Name>>(), new HashSet<Guid>());

    /// <summary>
    ///     The players this viewer may see even when they are private. Public players are a
    ///     predicate, not a member of this set — a search asks for "public or in this set".
    /// </summary>
    public IReadOnlySet<Guid> VisibleUserIds { get; } = Build(ViewerId, SharedCommunitiesByMember, RivalTargetIds);

    private static IReadOnlySet<Guid> Build(Guid? viewerId,
        IReadOnlyDictionary<Guid, IReadOnlyList<Name>> sharedCommunitiesByMember, IReadOnlySet<Guid> rivalTargetIds)
    {
        var set = new HashSet<Guid>(sharedCommunitiesByMember.Keys);
        set.UnionWith(rivalTargetIds);
        if (viewerId is { } viewer) set.Add(viewer);
        return set;
    }

    public PlayerVisibility Describe(Guid targetId, bool targetIsPublic)
    {
        var isYou = ViewerId == targetId;
        var isRival = RivalTargetIds.Contains(targetId);
        var shared = SharedCommunitiesByMember.TryGetValue(targetId, out var communities)
            ? communities
            : Array.Empty<Name>();
        var canView = isYou || targetIsPublic || isRival || shared.Count > 0;
        return new PlayerVisibility(canView, isYou, targetIsPublic, isRival, shared);
    }
}
