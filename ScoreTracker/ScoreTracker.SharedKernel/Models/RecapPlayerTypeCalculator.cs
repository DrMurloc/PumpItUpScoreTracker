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

    // The cutoffs are tuned raw score ranges, deliberately pinned rather than derived
    // from any mix's grade table: Phoenix 2 re-cut the sub-AAA grade floors, and a future
    // re-cut must never silently re-tune who counts as which type. The values coincide
    // with the AAA / S / SS / SSS+ floors, which are identical in every Phoenix-family mix.
    private const int PassRefinerFloor = 950_000;
    private const int BalancedFloor = 970_000;
    private const int CompetitiveFloor = 980_000;
    private const int PerfectionistFloor = 995_000;

    /// <summary>
    ///     Bands over the average of the player's top-50 Pumbility scores:
    ///     under 950k / 950k–970k / 970k–980k / 980k–995k / 995k and up.
    /// </summary>
    public static RecapPlayerType? Calculate(IReadOnlyCollection<PhoenixScore> topPumbilityScores)
    {
        if (topPumbilityScores.Count < MinimumScores) return null;

        var average = topPumbilityScores.Average(s => (int)s);
        if (average >= PerfectionistFloor) return RecapPlayerType.Perfectionist;
        if (average >= CompetitiveFloor) return RecapPlayerType.Competitive;
        if (average >= BalancedFloor) return RecapPlayerType.BalancedPlayer;
        if (average >= PassRefinerFloor) return RecapPlayerType.PassRefiner;
        return RecapPlayerType.PassPusher;
    }
}
