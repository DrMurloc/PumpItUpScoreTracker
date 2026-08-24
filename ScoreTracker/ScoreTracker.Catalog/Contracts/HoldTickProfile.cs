using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One mix's hold-tick picture (docs/design/phoenix-score-calculator.md D11–D13): per level,
///     how much of a chart's judgement count is hold ticks — perfects for as long as the hold is
///     held — plus the extremes. Derived, never crawled: a chart's ticks are its judged note
///     count minus its simfile tap rows, so the numbers are estimates and the page says so.
///     Aggregate-only by owner ruling (D12): no per-chart surface promises a split.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HoldTickProfile(
    IReadOnlyList<HoldTickLevelStat> Levels,
    IReadOnlyList<HoldTickChartStat> MostTicks,
    IReadOnlyList<HoldTickChartStat> FewestTicksFifteenPlus,
    int ChartsMeasured);

/// <summary>One level's tick-share spread over its measured Singles and Doubles.</summary>
[ExcludeFromCodeCoverage]
public sealed record HoldTickLevelStat(int Level, int Charts, double MedianShare, double P10Share,
    double P90Share);

/// <summary>One extreme chart — the lists' rows, not a per-chart lookup surface.</summary>
[ExcludeFromCodeCoverage]
public sealed record HoldTickChartStat(Guid ChartId, string SongName, ChartType Type, int Level,
    int NoteCount, int HoldTicks, double Share);
