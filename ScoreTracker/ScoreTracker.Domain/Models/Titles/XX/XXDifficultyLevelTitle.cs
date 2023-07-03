using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.XX;

public sealed class XXDifficultyLevelTitle : XXTitle
{
    private readonly DifficultyLevel _maximumLevel;
    private readonly DifficultyLevel _minimumLevel;

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