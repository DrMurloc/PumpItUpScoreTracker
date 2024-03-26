using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixSkillTitle : PhoenixTitle
{
    private readonly Name _songName;
    private readonly ChartType _chartType;
    private readonly DifficultyLevel _level;

    public PhoenixSkillTitle(Name skill, int skillLevel, Name songName, ChartType chartType, DifficultyLevel level,
        PhoenixLetterGrade letterRequirement = PhoenixLetterGrade.SSS) : base(
        $"[{skill}] Lv.{skillLevel}",
        $"Get {letterRequirement.GetName()} on {songName} {chartType.GetShortHand()}{level}", "Skill",
        990000)
    {
        _songName = songName;
        _chartType = chartType;
        _level = level;
    }

    public bool MatchesChart(Chart chart)
    {
        return chart.Song.Name == _songName && _chartType == chart.Type && _level == chart.Level;
    }

    public override double CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
    {
        if (chart.Song.Name == _songName && _chartType == chart.Type && _level == chart.Level &&
            attempt.Score != null) return attempt.Score.Value;

        return 0;
    }
}