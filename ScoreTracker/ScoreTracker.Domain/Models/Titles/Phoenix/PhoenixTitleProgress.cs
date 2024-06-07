using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixTitleProgress : TitleProgress
{
    public PhoenixTitleProgress(PhoenixTitle title) : base(title)
    {
        PhoenixTitle = title;
        if (title is PhoenixDifficultyTitle dt) RequiredAaCount = dt.RequiredRating / dt.Level.BaseRating;
    }

    public PhoenixTitle PhoenixTitle { get; }

    public override double CompletionCount { get; protected set; }

    public int RequiredAaCount { get; }

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

    private readonly IDictionary<PhoenixLetterGrade, int> _paragonProgress = Enum.GetValues<PhoenixLetterGrade>()
        .ToDictionary(l => l, l => 0);

    public PhoenixLetterGrade? ParagonLevel => _paragonProgress.Where(kv => kv.Value >= RequiredAaCount)
        .OrderByDescending(kv => kv.Key).Select(kv => (PhoenixLetterGrade?)kv.Key).FirstOrDefault();

    public int NextParagonProgress => ParagonLevel == null ? _paragonProgress[PhoenixLetterGrade.F]
        : ParagonLevel == PhoenixLetterGrade.SSSPlus ? -1
        : _paragonProgress[ParagonLevel!.Value + 1];

    public void ApplyAttempt(Chart chart, RecordedPhoenixScore attempt)
    {
        var progress = PhoenixTitle.CompletionProgress(chart, attempt);
        CompletionCount += progress;
        if (RequiredAaCount <= 0 || !(progress > 0) || attempt.Score == null) return;

        for (var i = PhoenixLetterGrade.F; i <= attempt.Score!.Value.LetterGrade; i++) _paragonProgress[i]++;
    }
}