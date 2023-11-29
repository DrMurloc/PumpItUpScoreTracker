using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixTitleProgress : TitleProgress
{
    public PhoenixTitleProgress(PhoenixTitle title) : base(title)
    {
        PhoenixTitle = title;
    }

    public PhoenixTitle PhoenixTitle { get; }

    public override int CompletionCount { get; protected set; }

    public override string AdditionalNote
    {
        get
        {
            if (PhoenixTitle is not PhoenixDifficultyTitle difficultyTitle) return string.Empty;

            if (Title.CompletionRequired <= CompletionCount) return string.Empty;

            var min = Math.Ceiling((Title.CompletionRequired - CompletionCount) /
                                   (PhoenixLetterGrade.SSSPlus.GetModifier() * difficultyTitle.Level.BaseRating));
            var max = Math.Ceiling((Title.CompletionRequired - CompletionCount) /
                                   (PhoenixLetterGrade.AA.GetModifier() * difficultyTitle.Level.BaseRating));
            return $"{min}-{max} Passes, assuming AA or higher";
        }
    }

    public void ApplyAttempt(Chart chart, RecordedPhoenixScore attempt)
    {
        CompletionCount += PhoenixTitle.CompletionProgress(chart, attempt);
    }
}
