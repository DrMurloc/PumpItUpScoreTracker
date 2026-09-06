using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The viewer against their peers, for the Breakdown page's card
///     (docs/design/pumbility-overhaul.md D58): by level per type, and by chart type over the
///     merged fifty.
/// </summary>
/// <param name="Levels">
///     Per lit type in scope, where the viewer's fifty of the type sits against the peers' by
///     level (D41). Empty for a viewer with no lit type.
/// </param>
/// <param name="Peers">
///     The peers' average merged fifty split by type — the union of the lit types' peers, each
///     one's records of both types priced, merged, the top fifty taken, only a full fifty
///     counting. Null for a type scope, which is one type by definition, and where no peer
///     holds a full fifty.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityPoolCompareRecord(
    IReadOnlyDictionary<ChartType, PeerCompare> Levels,
    PoolTypeSplit? Peers)
{
    /// <summary>The answer for a viewer with no lit type: nothing to compare against.</summary>
    public static PumbilityPoolCompareRecord Empty { get; } =
        new(new Dictionary<ChartType, PeerCompare>(), null);
}

/// <summary>
///     Where the viewer's pool sits against the peers' by level (D41). The in-common, held-by-one
///     and yours-alone counts were computed here too until the field test cut the tiles that
///     printed them — a count nobody can act on is not worth a read.
/// </summary>
/// <param name="MyLevels">The viewer's pool charts per level.</param>
/// <param name="PeerShareByLevel">The peers' prevalence points per level, as a share of the type's total.</param>
[ExcludeFromCodeCoverage]
public sealed record PeerCompare(
    IReadOnlyDictionary<int, int> MyLevels,
    IReadOnlyDictionary<int, double> PeerShareByLevel);
