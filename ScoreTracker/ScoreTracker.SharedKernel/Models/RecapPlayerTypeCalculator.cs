using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

public static class RecapPlayerTypeCalculator
{
    /// <summary>
    ///     Below this many top-Pumbility scores an average grade says more about
    ///     sample size than play style, so no type is assigned.
    /// </summary>
    public const int MinimumScores = 10;

    /// <summary>
    ///     Phoenix's bands: AAA / S / SS / SSS+, the floors of its own grade table.
    /// </summary>
    private static readonly int[] PhoenixFloors = { 950_000, 970_000, 980_000, 995_000 };

    /// <summary>
    ///     Phoenix 2's bands: S / S+ / SS / SS+, the floors of ITS grade table, which re-cut
    ///     everything below AAA and packed the live range into four rungs. One rung per type, so
    ///     the archetype is a sentence — a fifty averaging an S+ is a Balanced Player
    ///     (docs/design/pumbility-overhaul.md D69, measured in §4.15).
    /// </summary>
    private static readonly int[] Phoenix2Floors = { 970_000, 975_000, 980_000, 985_000 };

    /// <summary>
    ///     The cutoffs a mix bands on. They are pinned rather than read out of
    ///     <see cref="PhoenixLetterGradeHelperMethods.GetMinimumScoreFor" /> deliberately: the two
    ///     tables agreeing today is the point, and a future grade re-cut must be a decision about
    ///     archetypes rather than something that silently re-tunes who is which type. Phoenix 2's
    ///     own re-cut is exactly that story — it moved the grade floors in 2026-07 and the
    ///     archetypes went on reading Phoenix 1's numbers until they were re-measured.
    /// </summary>
    private static int[] FloorsFor(MixEnum mix)
    {
        return mix == MixEnum.Phoenix2 ? Phoenix2Floors : PhoenixFloors;
    }

    /// <summary>
    ///     Bands over the average of the player's top-Pumbility scores. There is deliberately no
    ///     mix-less form — the same rule <see cref="PhoenixLetterGradeHelperMethods.LetterGradeFor" />
    ///     follows, since a Phoenix 2 fifty read on Phoenix's floors lands two archetypes low.
    /// </summary>
    public static RecapPlayerType? Calculate(IReadOnlyCollection<PhoenixScore> topPumbilityScores, MixEnum mix)
    {
        if (topPumbilityScores.Count < MinimumScores) return null;

        return FromAverage(topPumbilityScores.Average(s => (int)s), mix);
    }

    /// <summary>
    ///     Bands a precomputed top-Pumbility average — for callers that persist the average
    ///     (player stats) instead of the raw score list. The caller owns the
    ///     <see cref="MinimumScores" /> sample-size guard.
    /// </summary>
    public static RecapPlayerType FromAverage(double average, MixEnum mix)
    {
        var floors = FloorsFor(mix);
        if (average >= floors[3]) return RecapPlayerType.Perfectionist;
        if (average >= floors[2]) return RecapPlayerType.Competitive;
        if (average >= floors[1]) return RecapPlayerType.BalancedPlayer;
        if (average >= floors[0]) return RecapPlayerType.PassRefiner;
        return RecapPlayerType.PassPusher;
    }

    /// <summary>
    ///     The lowest average that earns <paramref name="type" /> in this mix, or null for
    ///     <see cref="RecapPlayerType.PassPusher" />, which has no floor beneath it. What a surface
    ///     naming the bands reads, so the copy and the banding cannot drift apart.
    /// </summary>
    public static int? FloorFor(RecapPlayerType type, MixEnum mix)
    {
        return type == RecapPlayerType.PassPusher ? null : FloorsFor(mix)[(int)type - 1];
    }
}
