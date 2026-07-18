using ScoreTracker.Domain.Models.Titles.Interface;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     A Phoenix 2 skill title: reach a minimum letter grade on one specific chart (the
///     TWIST/RUN/DRILL/GIMMICK/SLOW/HALF/BRACKET ladders, plus the chart-specific grade
///     badges). Song names follow the official requirement text — a title whose song isn't
///     in the Phoenix 2 catalog yet simply never progresses until the song imports.
/// </summary>
public sealed class Phoenix2ChartGradeTitle : PhoenixTitle, ISpecificChartTitle
{
    private readonly ChartType _chartType;
    private readonly DifficultyLevel _level;
    private readonly PhoenixLetterGrade _minimumGrade;
    private readonly Name _songName;

    public Phoenix2ChartGradeTitle(Name name, Name category, Name songName, ChartType chartType,
        DifficultyLevel level, PhoenixLetterGrade minimumGrade) : base(name,
        $"{songName} {chartType.GetShortHand()}{(int)level} — {minimumGrade.GetName()} or better", category, 1)
    {
        _songName = songName;
        _chartType = chartType;
        _level = level;
        _minimumGrade = minimumGrade;
    }

    public override bool PopulatesFromDatabase => false;

    public override double CompletionProgress(Chart chart, RecordedPhoenixScore attempt)
    {
        return AppliesToChart(chart) && !attempt.IsBroken && attempt.Score != null &&
               attempt.Score.Value.LetterGradeFor(MixEnum.Phoenix2) >= _minimumGrade
            ? 1
            : 0;
    }

    public bool AppliesToChart(Chart chart)
    {
        return chart.Song.Name == _songName && _chartType == chart.Type && _level == chart.Level;
    }
}
