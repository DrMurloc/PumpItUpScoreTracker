using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.XX;

public sealed class XXSkillTitle : XXTitle
{
    private readonly ChartType _chartType;
    private readonly XXLetterGrade _letterGrade;
    private readonly DifficultyLevel _level;
    private readonly Name _songName;

    public XXSkillTitle(Name title, Name songName, ChartType chartType, DifficultyLevel difficultyLevel,
        XXLetterGrade letterGrade) : base(title,
        $"Achieve {letterGrade} on {songName} {chartType.GetShortHand()}{difficultyLevel}", "Skill")
    {
        _songName = songName;
        _chartType = chartType;
        _level = difficultyLevel;
        _letterGrade = letterGrade;
    }

    public XXSkillTitle(Name title, Name songName, ChartType chartType, DifficultyLevel difficultyLevel) : this(
        title, songName, chartType, difficultyLevel, XXLetterGrade.SS)
    {
    }

    public override bool DoesAttemptApply(BestXXChartAttempt attempt)
    {
        if (attempt.BestAttempt == null) return false;

        return attempt.Chart.Song.Name == _songName && attempt.Chart.Level == _level &&
               attempt.Chart.Type == _chartType && attempt.BestAttempt.LetterGrade >= _letterGrade;
    }
}