using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     How a cohort's fifties fall across the five playstyle archetypes, before a viewer is placed
///     on it (docs/design/pumbility-overhaul.md D69). Viewer-free for the same reason
///     <see cref="CohortLevelSpread" /> is: every player standing on a band is looking at the same
///     population, so the band is read once and shared.
/// </summary>
/// <param name="Holders">
///     Everyone standing on the band whose fifty could be banded, board players among them. A
///     holder with no priceable record has no average and is not one of these.
/// </param>
/// <param name="HoldersByArchetype">
///     How many of them land in each archetype, every one of the five present even at zero — an
///     absent archetype is a real answer about the band and the drawing needs its width.
/// </param>
public sealed record CohortArchetypeSpread(
    int Holders,
    IReadOnlyDictionary<RecapPlayerType, int> HoldersByArchetype)
{
    /// <summary>Nobody on the band had a fifty to band.</summary>
    public static CohortArchetypeSpread Empty { get; } = new(0, Empties());

    private static Dictionary<RecapPlayerType, int> Empties()
    {
        return Enum.GetValues<RecapPlayerType>().ToDictionary(type => type, _ => 0);
    }

    /// <summary>
    ///     The band's own answer, over the fifties the pool summary already measured. The mix picks
    ///     the cutoffs and is never inferred — a Phoenix 2 fifty read on Phoenix's floors lands two
    ///     archetypes low.
    /// </summary>
    public static CohortArchetypeSpread Of(PeerPoolSummary summary, MixEnum mix)
    {
        var counts = Empties();
        var holders = 0;
        foreach (var peer in summary.Peers)
        {
            if (!summary.AveragesByPeer.TryGetValue(peer, out var average)) continue;
            counts[RecapPlayerTypeCalculator.FromAverage(average, mix)]++;
            holders++;
        }

        return new CohortArchetypeSpread(holders, counts);
    }
}

/// <summary>
///     The same spread with the viewer placed on it — what the Breakdown card's archetype section
///     draws (docs/design/pumbility-overhaul.md D69, §3.6).
///     <para>
///         The archetypes are five ways to hold a fifty and none of them is above another, so
///         nothing here is a rank and nothing reading it may phrase one: there is no percentile
///         and no "better than" — <see cref="StandingWithMe" /> is how many share the viewer's
///         band, and <see cref="Ahead" /> is only ever the position the marker is drawn at.
///     </para>
/// </summary>
/// <param name="Holders">Everyone on the band whose fifty could be banded.</param>
/// <param name="BoardHolders">How many of those the official board is the only record of.</param>
/// <param name="HoldersByArchetype">The five counts, every archetype present even at zero.</param>
/// <param name="MyAverage">
///     The viewer's own top-50 average, or null when they hold too short a pool to band — the
///     section still draws the cohort, with no marker on it.
/// </param>
/// <param name="Mine">The archetype that average lands in, null with no average.</param>
public sealed record ArchetypeSpread(
    int Holders,
    int BoardHolders,
    IReadOnlyDictionary<RecapPlayerType, int> HoldersByArchetype,
    double? MyAverage,
    RecapPlayerType? Mine)
{
    /// <summary>Nothing to draw.</summary>
    public static ArchetypeSpread Empty { get; } =
        new(0, 0, CohortArchetypeSpread.Empty.HoldersByArchetype, null, null);

    /// <summary>How many of the cohort stand in the viewer's own archetype, the viewer included.</summary>
    public int StandingWithMe => Mine is { } mine ? HoldersByArchetype.GetValueOrDefault(mine) : 0;

    /// <summary>
    ///     Where along the five bands the viewer's marker sits, on 0..1 — the share of the cohort
    ///     whose fifty averages less. It is a POSITION on the spectrum, not a standing: the bands
    ///     are drawn at their own widths in this order, so a marker placed anywhere else would sit
    ///     outside the band it belongs to. Null with no average.
    /// </summary>
    public double? Ahead { get; init; }

    /// <summary>
    ///     The share of the cohort in one archetype, on 0..1. Zero when nobody is banded, which is
    ///     the honest width for a drawing with no population behind it.
    /// </summary>
    public double ShareOf(RecapPlayerType type)
    {
        return Holders == 0 ? 0 : (double)HoldersByArchetype.GetValueOrDefault(type) / Holders;
    }

    /// <summary>
    ///     The cohort's spread with a viewer's own fifty laid over it. <paramref name="myScores" />
    ///     is the viewer's pool as the page already built it; too short a one leaves the marker off
    ///     rather than banding a handful of charts, the same guard the chip applies.
    /// </summary>
    public static ArchetypeSpread Of(CohortArchetypeSpread cohort, int boardHolders,
        IReadOnlyCollection<int> myScores, MixEnum mix)
    {
        var mine = myScores.Count >= RecapPlayerTypeCalculator.MinimumScores
            ? myScores.Average()
            : (double?)null;
        return new ArchetypeSpread(cohort.Holders, boardHolders, cohort.HoldersByArchetype, mine,
            mine is { } average ? RecapPlayerTypeCalculator.FromAverage(average, mix) : null)
        {
            Ahead = mine is { } own && cohort.Holders > 0 ? AheadOf(cohort, own, mix) : null
        };
    }

    /// <summary>
    ///     Where the marker goes: the bands below the viewer's in full, plus the viewer's own band
    ///     split at where they sit inside it. The counts are all the cohort is held at, so the share
    ///     inside a band is read off the viewer's own position between its floor and the next —
    ///     enough to keep the marker inside its band and off the seams, which is all the drawing
    ///     asks of it.
    /// </summary>
    private static double AheadOf(CohortArchetypeSpread cohort, double average, MixEnum mix)
    {
        var mine = RecapPlayerTypeCalculator.FromAverage(average, mix);
        var below = Enum.GetValues<RecapPlayerType>().Where(type => type < mine)
            .Sum(type => cohort.HoldersByArchetype.GetValueOrDefault(type));
        var floor = RecapPlayerTypeCalculator.FloorFor(mine, mix);
        var ceiling = mine == RecapPlayerType.Perfectionist
            ? null
            : RecapPlayerTypeCalculator.FloorFor(mine + 1, mix);

        // Inside the band, linearly between its edges. The top band is open, so a Perfectionist
        // sits at its midpoint rather than running off the end of a strip that has to stop.
        var within = floor is { } from && ceiling is { } to && to > from
            ? Math.Clamp((average - from) / (to - from), 0, 1)
            : 0.5;
        return (below + within * cohort.HoldersByArchetype.GetValueOrDefault(mine)) / cohort.Holders;
    }
}
