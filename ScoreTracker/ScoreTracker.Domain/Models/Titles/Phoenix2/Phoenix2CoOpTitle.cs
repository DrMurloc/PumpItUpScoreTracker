using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     A Phoenix 2 title earned by reaching a CO-OP Rating — the thirteen-rung [CO-OP] ladder,
///     Lv.1 at 1,000 through MASTER at 16,000. The rating is every co-op chart's best
///     non-broken score at 80 × (grade + plate), summed: not a top-50 pool, so it accumulates
///     attempt by attempt like <see cref="PhoenixCoOpTitle" /> does on Phoenix, and a broken
///     play adds nothing. Prices through the mix's own configuration so the constant tables
///     live in one place; the site's requirement text is kept verbatim as the description.
/// </summary>
public sealed class Phoenix2CoOpTitle : PhoenixTitle
{
    private static readonly ScoringConfiguration Scoring =
        ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, true);

    public Phoenix2CoOpTitle(Name name, string description, int ratingRequired, int rung)
        : base(name, description, "CO-OP", ratingRequired)
    {
        RequiredRating = ratingRequired;
        OnRail("CO-OP", rung);
    }

    public int RequiredRating { get; }

    public override double CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
    {
        if (chart.Type != ChartType.CoOp || attempt.IsBroken || attempt.Score == null) return 0;
        return Scoring.GetScore(chart.Type, chart.Level, attempt.Score.Value,
            attempt.Plate ?? PhoenixPlate.RoughGame, attempt.IsBroken);
    }

    /// <summary>
    ///     What one more co-op chart is worth at the two ends of the realistic range — a perfect
    ///     and a bare AA — for the "passes to go" hint.
    /// </summary>
    public static (double Best, double Least) ContributionRange()
    {
        return (Scoring.GetScore(ChartType.CoOp, DifficultyLevel.From(2), PhoenixScore.From(1_000_000),
                PhoenixPlate.PerfectGame),
            Scoring.GetScore(ChartType.CoOp, DifficultyLevel.From(2),
                PhoenixLetterGrade.AA.GetMinimumScoreFor(MixEnum.Phoenix2), PhoenixPlate.RoughGame));
    }
}
