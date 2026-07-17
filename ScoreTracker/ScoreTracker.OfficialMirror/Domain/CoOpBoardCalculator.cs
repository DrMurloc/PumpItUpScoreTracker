using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Prices co-op board placements for the reverse-engineered CO-OP ranking. No official
///     CO-OP rating exists, so this estimates one with the mix's own PUMBILITY formula:
///     the engine's flat co-op base × (grade + plate), with the plate inferred from the
///     score alone — chart boards expose no plates. The flat base makes the ranking order
///     independent of the base's value; only the displayed magnitude depends on it.
/// </summary>
internal static class CoOpBoardCalculator
{
    // Co-op pricing ignores the level argument entirely (the engine's co-op base is flat),
    // but the scoring API requires one.
    private static readonly DifficultyLevel PlaceholderLevel = DifficultyLevel.From(10);

    /// <summary>
    ///     The mix's PUMBILITY formula with co-op charts counted instead of zeroed —
    ///     the official Phoenix 2 formula excludes co-op, this estimate deliberately doesn't.
    /// </summary>
    public static ScoringConfiguration EstimateScoring(MixEnum mix)
    {
        var scoring = ScoringConfiguration.PumbilityScoring(mix, true);
        scoring.ChartTypeModifiers[ChartType.CoOp] = 1.0;
        return scoring;
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

    public static int Rating(ScoringConfiguration estimateScoring, PhoenixScore score)
    {
        return (int)estimateScoring.GetScore(ChartType.CoOp, PlaceholderLevel, score, InferredPlate(score));
    }
}
