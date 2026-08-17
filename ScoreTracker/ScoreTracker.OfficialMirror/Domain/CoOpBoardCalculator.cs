using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Prices co-op board placements for the CO-OP ranking. PIUGAME publishes no CO-OP
///     leaderboard, so this builds one from the mirrored co-op chart boards with the mix's own
///     CO-OP Rating formula — the engine's flat co-op base × (grade + plate), 2000 per chart on
///     Phoenix and 80 on Phoenix 2 — with the plate inferred from the score alone, since chart
///     boards expose no plates. The number is a lower bound on the account's real rating rather
///     than a guess at a different one: a chart board lists only its top 300, so a chart the
///     player is not top-300 on never reaches the sum, and an inferred plate can only sit under
///     the real one where the two differ.
/// </summary>
internal static class CoOpBoardCalculator
{
    // A co-op's "level" is its player count and the flat base ignores it entirely, but the
    // scoring API requires one.
    private static readonly DifficultyLevel PlaceholderLevel = DifficultyLevel.From(2);

    /// <summary>
    ///     The mix's PUMBILITY formula with co-op charts counted — the same configuration
    ///     <c>PlayerRatingSaga</c> sums an account's own CO-OP Rating with, so a board row and
    ///     the account price a chart identically.
    /// </summary>
    public static ScoringConfiguration EstimateScoring(MixEnum mix)
    {
        return ScoringConfiguration.PumbilityScoring(mix, true);
    }

    /// <summary>
    ///     The plate assumed for a bare board score: SG below 995,000, UG from there up,
    ///     and PG only at a perfect 1,000,000.
    /// </summary>
    public static PhoenixPlate InferredPlate(PhoenixScore score)
    {
        return (int)score switch
        {
            >= 1_000_000 => PhoenixPlate.PerfectGame,
            >= 995_000 => PhoenixPlate.UltimateGame,
            _ => PhoenixPlate.SuperbGame
        };
    }

    public static double Rating(ScoringConfiguration estimateScoring, PhoenixScore score)
    {
        return estimateScoring.GetScore(ChartType.CoOp, PlaceholderLevel, score, InferredPlate(score));
    }
}
