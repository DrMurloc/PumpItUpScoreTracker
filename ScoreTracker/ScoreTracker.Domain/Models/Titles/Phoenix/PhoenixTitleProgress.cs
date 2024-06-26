﻿using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixTitleProgress : TitleProgress
{
    public PhoenixTitleProgress(PhoenixTitle title) : base(title)
    {
        PhoenixTitle = title;
        if (title is PhoenixDifficultyTitle dt)
            RequiredAaCount = (int)Math.Ceiling((double)dt.RequiredRating / dt.Level.BaseRating);
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

    private readonly IDictionary<ParagonLevel, int> _paragonProgress = Enum.GetValues<ParagonLevel>()
        .ToDictionary(l => l, l => 0);

    public ParagonLevel ParagonLevel => RequiredAaCount == 0
        ? ParagonLevel.None
        : _paragonProgress.Where(kv => kv.Value >= RequiredAaCount)
            .OrderByDescending(kv => kv.Key).Select(kv => (ParagonLevel?)kv.Key).FirstOrDefault() ?? ParagonLevel.None;

    public int NextParagonProgress => ParagonLevel == ParagonLevel.None ? _paragonProgress[ParagonLevel.F]
        : ParagonLevel == ParagonLevel.PG ? -1
        : _paragonProgress[ParagonLevel + 1];


    public void ApplyAttempt(Chart chart, RecordedPhoenixScore attempt)
    {
        var progress = PhoenixTitle.CompletionProgress(chart, attempt);
        CompletionCount += progress;
        if (RequiredAaCount <= 0 || !(progress > 0) || attempt.Score == null) return;

        for (var i = ParagonLevel.F; i <= attempt.Score!.Value.GetParagonLevel(); i++) _paragonProgress[i]++;
    }
}