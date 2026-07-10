using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Domain.Recap;

internal static class RecapPlayerTypeCalculator
{
    /// <summary>
    ///     Below this many top-Pumbility scores an average grade says more about
    ///     sample size than play style, so no type is assigned.
    /// </summary>
    public const int MinimumScores = 10;

    /// <summary>
    ///     Bands are letter-grade ranges over the average of the player's top-50
    ///     Pumbility scores: ≤AA+ / AAA–AAA+ / S–S+ / SS–SSS / SSS+.
    /// </summary>
    public static RecapPlayerType? Calculate(IReadOnlyCollection<PhoenixScore> topPumbilityScores)
    {
        if (topPumbilityScores.Count < MinimumScores) return null;

        var average = topPumbilityScores.Average(s => (int)s);
        if (average >= (int)PhoenixLetterGrade.SSSPlus.GetMinimumScore()) return RecapPlayerType.Perfectionist;
        if (average >= (int)PhoenixLetterGrade.SS.GetMinimumScore()) return RecapPlayerType.Competitive;
        if (average >= (int)PhoenixLetterGrade.S.GetMinimumScore()) return RecapPlayerType.BalancedPlayer;
        if (average >= (int)PhoenixLetterGrade.AAA.GetMinimumScore()) return RecapPlayerType.PassRefiner;
        return RecapPlayerType.PassPusher;
    }
}
