using ScoreTracker.SharedKernel.Models;
﻿using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixDifficultyTitle : PhoenixTitle
{
    /// <param name="tier">
    ///     The rail this draws on. Optional only so tests can build a bare title; every entry
    ///     in <see cref="PhoenixTitleList" /> declares one, and TitleRailTests fails any
    ///     computed title that ends up on no rail.
    /// </param>
    public PhoenixDifficultyTitle(Name name, DifficultyLevel level, int ratingRequired, Name? tier = null) : base(name,
        $"Get {ratingRequired} Rating on {level}s ({level.BaseRating} per AA)", "Difficulty", ratingRequired)
    {
        Level = level;
        RequiredRating = ratingRequired;
        Tier = tier;
    }

    public DifficultyLevel Level { get; }
    public int RequiredRating { get; }

    /// <summary>
    ///     The folder tier the player reads this as — Intermediate, Advanced, Expert, The
    ///     Master — and the rail it draws on. Not derivable from <see cref="Level" />:
    ///     Advanced spans the 20s through the 22s and Expert the 23s through the 27s.
    /// </summary>
    public Name? Tier { get; }
    public override bool PopulatesFromDatabase => false;

    public override double CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
    {
        if (chart.Level != Level || attempt.IsBroken || attempt.Score == null) return 0;
        return chart.Level.BaseRating * attempt.Score.Value.LetterGradeFor(chart.Mix).GetModifier();
    }
}