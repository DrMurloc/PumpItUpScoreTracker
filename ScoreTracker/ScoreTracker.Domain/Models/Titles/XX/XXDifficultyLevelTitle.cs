using ScoreTracker.SharedKernel.Models;
﻿using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.XX;

public sealed class XXDifficultyLevelTitle : XXTitle
{
    private readonly DifficultyLevel _maximumLevel;
    private readonly DifficultyLevel _minimumLevel;

    /// <summary>The folder this title counts passes in — two titles over the same range are one ladder.</summary>
    public (int Minimum, int Maximum) LevelRange => (_minimumLevel, _maximumLevel);

    public XXDifficultyLevelTitle(Name title, DifficultyLevel minimumLevel, DifficultyLevel maximumLevel,
        int requiredCount) : base(title,
        $"{requiredCount} stage passes on {minimumLevel}-{maximumLevel}s, except Missions", "Difficulty", requiredCount)
    {
        _minimumLevel = minimumLevel;
        _maximumLevel = maximumLevel;
    }

    public XXDifficultyLevelTitle(Name title, DifficultyLevel level, int requiredCount) : base(title,
        $"{requiredCount} stage passes on {level}s, except Missions", "Difficulty", requiredCount)
    {
        _minimumLevel = level;
        _maximumLevel = level;
    }

    public XXDifficultyLevelTitle(Name title, DifficultyLevel level, int requiredCount, string additionalRequirements) :
        base(title,
            $"{requiredCount} stage passes on {level}s, except Missions. {additionalRequirements}", "Difficulty",
            requiredCount)
    {
        _minimumLevel = level;
        _maximumLevel = level;
    }

    public override bool DoesAttemptApply(BestXXChartAttempt attempt)
    {
        if (attempt.BestAttempt == null) return false;

        return !attempt.BestAttempt.IsBroken && attempt.Chart.Level >= _minimumLevel &&
               attempt.Chart.Level <= _maximumLevel;
    }
}