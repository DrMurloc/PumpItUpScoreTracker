using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixCoOpTitle : PhoenixTitle
{
    public PhoenixCoOpTitle(Name name, int ratingRequired) : base(name,
        $"Get {ratingRequired} Rating on CoOps (2000 per AA)", "CoOp", ratingRequired)
    {
        RequiredRating = ratingRequired;
    }

    public int RequiredRating { get; }

    public override double CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
    {
        if (chart.Type != ChartType.CoOp || attempt.IsBroken || attempt.Score == null) return 0;
        return 2000 * attempt.Score.Value.LetterGrade.GetModifier();
    }
}