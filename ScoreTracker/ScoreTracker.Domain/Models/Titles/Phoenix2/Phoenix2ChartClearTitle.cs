using ScoreTracker.Domain.Models.Titles.Interface;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     A Phoenix 2 boss-breaker title: clear one specific chart ("ROUGH GAME or more" on the
///     official page = any unbroken pass). A null level matches the song+type at any level —
///     the [PHOENIX] double boss renders a "??" stepball with no parseable level.
/// </summary>
public sealed class Phoenix2ChartClearTitle : PhoenixTitle, ISpecificChartTitle
{
    private readonly ChartType _chartType;
    private readonly DifficultyLevel? _level;
    private readonly Name _songName;

    public Phoenix2ChartClearTitle(Name name, Name songName, ChartType chartType, DifficultyLevel? level)
        : base(name,
            $"Clear {songName} {chartType.GetShortHand()}{(level == null ? "??" : ((int)level.Value).ToString())}",
            "Boss Breaker", 1)
    {
        _songName = songName;
        _chartType = chartType;
        _level = level;
    }

    public override bool PopulatesFromDatabase => false;

    public override double CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
    {
        return AppliesToChart(chart) && !attempt.IsBroken ? 1 : 0;
    }

    public bool AppliesToChart(Chart chart)
    {
        return chart.Song.Name == _songName && _chartType == chart.Type &&
               (_level == null || _level.Value == chart.Level);
    }
}
