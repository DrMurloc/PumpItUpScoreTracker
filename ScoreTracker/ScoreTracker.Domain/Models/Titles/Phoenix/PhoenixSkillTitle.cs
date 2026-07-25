using ScoreTracker.SharedKernel.Models;
﻿using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models.Titles.Interface;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix;

public sealed class PhoenixSkillTitle : PhoenixTitle, ISpecificChartTitle
{
    private readonly ChartType _chartType;
    private readonly DifficultyLevel _level;

    public PhoenixSkillTitle(Name skill, int skillLevel, Name songName, ChartType chartType, DifficultyLevel level,
        PhoenixLetterGrade letterRequirement = PhoenixLetterGrade.SSS) : base(
        $"[{skill}] Lv.{skillLevel}",
        $"Get {letterRequirement.GetName()} on {songName} {chartType.GetShortHand()}{level}", "Skill",
        990000)
    {
        SongName = songName;
        _chartType = chartType;
        _level = level;
        // Owner call: skill progress measures the climb from a decent pass (900k) to the
        // SSS, so a fresh pass doesn't read as nearly complete.
        FloorAt(900_000);
    }

    public Name SongName { get; }

    public override bool PopulatesFromDatabase => false;


    public override double CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
    {
        if (AppliesToChart(chart) &&
            attempt.Score != null) return attempt.Score.Value;

        return 0;
    }

    public bool AppliesToChart(Chart chart)
    {
        return chart.Song.Name == SongName && _chartType == chart.Type && _level == chart.Level;
    }
}