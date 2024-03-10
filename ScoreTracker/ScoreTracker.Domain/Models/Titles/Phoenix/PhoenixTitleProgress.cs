using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixTitleProgress : TitleProgress
{
    public PhoenixTitleProgress(PhoenixTitle title) : base(title)
    {
        PhoenixTitle = title;
    }

    public PhoenixTitle PhoenixTitle { get; }

    public override double CompletionCount { get; protected set; }

    public override string AdditionalNote
    {
        get
        {
            var rating = PhoenixTitle switch
            {
                PhoenixDifficultyTitle difficultyTitle => difficultyTitle.Level.BaseRating,
                PhoenixCoOpTitle => 2000,
                _ => 0
            };
            if (rating == 0) return string.Empty;

            if (Title.CompletionRequired <= CompletionCount) return string.Empty;
            var min = Math.Ceiling((Title.CompletionRequired - CompletionCount) /
                                   (PhoenixLetterGrade.SSSPlus.GetModifier() * rating));
            var max = Math.Ceiling((Title.CompletionRequired - CompletionCount) /
                                   (PhoenixLetterGrade.AA.GetModifier() * rating));
            return $"{min}-{max} Passes, assuming AA or higher";
        }
    }

    public void ApplyAttempt(Chart chart, RecordedPhoenixScore attempt)
    {
        CompletionCount += PhoenixTitle.CompletionProgress(chart, attempt);
    }
}