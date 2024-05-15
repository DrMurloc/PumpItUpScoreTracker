using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models.Titles.Interface;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixSkillTitle : PhoenixTitle, ISpecificChartTitle
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

    public override bool PopulatesFromDatabase => false;


    public override double CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
    {
        if (AppliesToChart(chart) &&
            attempt.Score != null) return attempt.Score.Value;

        return 0;
    }

    public bool AppliesToChart(Chart chart)
    {
        return chart.Song.Name == _songName && _chartType == chart.Type && _level == chart.Level;
    }
}