using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The players holding one title, for the Breakdown page's card
///     (docs/design/pumbility-overhaul.md D68): who they are, what their fifties are made of by
///     level, and — on the merged pool alone — what those fifties split into by chart type.
///     <para>
///         The viewer is one of them when they stand on the band themselves. That is what a census
///         of a title is, and it is also what makes the read the same for everyone on it: a cohort
///         that dropped whoever was asking would be a different answer per reader and could not be
///         shared.
///     </para>
/// </summary>
/// <param name="Band">
///     The band this answers for — the rung, or the gem it sits in where the level was too thin to
///     read (<see cref="PumbilityBand.MinimumForLevel" />), or Phoenix 1's difficulty title. Null
///     when the viewer stands under the ladder and asked for no band of their own.
/// </param>
/// <param name="Holders">How many players hold it, board players among them.</param>
/// <param name="BoardHolders">
///     How many of those the official board is the only record of. Always zero on Phoenix 1, whose
///     titles are rating earned on one level rather than a number a board publishes.
/// </param>
/// <param name="Spread">Their fifties by level, with the viewer's own count on each column.</param>
/// <param name="Split">
///     Their average merged fifty by chart type, for the merged pool only — a singles or doubles
///     pool is one type by definition. Null where no holder keeps a full fifty.
/// </param>
/// <param name="BoardAsOf">
///     When the board half was swept, so the card can say how old it is (peers-abstraction.md D37).
///     Null when no board player is counted.
/// </param>
/// <param name="Archetypes">
///     How their fifties fall across the five playstyle archetypes, and where the viewer's own
///     stands among them (docs/design/pumbility-overhaul.md D69). Null on a singles or doubles
///     pool: an archetype is the merged fifty's statement, the same rule
///     <paramref name="Split" /> follows, and banding a typed fifty would name an archetype the
///     player's own chip disagrees with.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityCohortRecord(
    Name? Band,
    int Holders,
    int BoardHolders,
    PeerLevelSpread Spread,
    PoolTypeSplit? Split,
    DateTimeOffset? BoardAsOf,
    ArchetypeSpread? Archetypes = null)
{
    /// <summary>The answer for a viewer with no band to read: nothing to compare against.</summary>
    public static PumbilityCohortRecord Empty { get; } =
        new(null, 0, 0, PeerLevelSpread.Empty, null, null);
}
