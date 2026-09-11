using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The viewer against their peers, for the Breakdown page's card
///     (docs/design/pumbility-overhaul.md D58): by level per type, and by chart type over the
///     merged fifty.
/// </summary>
/// <param name="Levels">
///     Per lit type in scope, how many charts of each level every peer keeps in their fifty of the
///     type, with the viewer's own count on it (D66). Empty for a viewer with no lit type.
/// </param>
/// <param name="Peers">
///     The peers' average merged fifty split by type — the union of the lit types' peers, each
///     one's records of both types priced, merged, the top fifty taken, only a full fifty
///     counting. Null for a type scope, which is one type by definition, and where no peer
///     holds a full fifty.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityPoolCompareRecord(
    IReadOnlyDictionary<ChartType, PeerLevelSpread> Levels,
    PoolTypeSplit? Peers)
{
    /// <summary>The answer for a viewer with no lit type: nothing to compare against.</summary>
    public static PumbilityPoolCompareRecord Empty { get; } =
        new(new Dictionary<ChartType, PeerLevelSpread>(), null);
}
