using ScoreTracker.Domain.Models.Titles.Interface;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     A Phoenix 2 boss-breaker title: clear one specific chart ("ROUGH GAME or more" on the
///     official page = any unbroken pass). Always one exact chart: a "??" stepball on the official
///     page is how the game displays that chart's level, not a wildcard, so matching the song+type
///     at any level would hand the title to anyone who cleared an easier chart of the same song.
/// </summary>
public sealed class Phoenix2ChartClearTitle : PhoenixTitle, ISpecificChartTitle
{
    private readonly ChartType _chartType;
    private readonly DifficultyLevel _level;

    public Phoenix2ChartClearTitle(Name name, Name songName, ChartType chartType, DifficultyLevel level)
        : base(name, $"Clear {songName} {chartType.GetShortHand()}{(int)level}", "Boss Breaker", 1)
    {
        SongName = songName;
        _chartType = chartType;
        _level = level;
    }

    public Name SongName { get; }

    public override bool PopulatesFromDatabase => false;

    public override double CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
    {
        return AppliesToChart(chart) && !attempt.IsBroken ? 1 : 0;
    }

    public bool AppliesToChart(Chart chart)
    {
        return chart.Song.Name == SongName && _chartType == chart.Type && _level == chart.Level;
    }
}
