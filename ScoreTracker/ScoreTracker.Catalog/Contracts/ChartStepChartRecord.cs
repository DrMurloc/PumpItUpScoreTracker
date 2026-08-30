namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One chart's renderable step timeline as one mix sees it
///     (docs/design/step-chart-failure-map.md §3): the strip's rows and holds in seconds (beats
///     and quantization riding where the .ssc aligned), the invisible tick times the position
///     solver indexes, the snapshot's own segment spans and ranges of interest, and the mix's
///     verdict. A chart verdicted <see cref="StepChartVisibility.Excluded" /> never surfaces as
///     one of these — the query answers null instead, and no section renders.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartStepChartRecord(
    string Vintage,
    int Panels,
    bool BeatsAligned,
    StepChartVisibility Visibility,
    int? NoteCount,
    int ImpliedTotal,
    IReadOnlyList<StepChartRowRecord> Rows,
    IReadOnlyList<StepChartHoldRecord> Holds,
    IReadOnlyList<decimal> TickTimes,
    IReadOnlyList<StepChartSegmentRecord> Segments,
    IReadOnlyList<StepChartRangeRecord> RangesOfInterest);

/// <summary>A judgement row: its second, every panel struck, the left foot's panels, and — when
/// beats aligned — the beat and its quantization (4/8/12/16/…, 0 = off-grid).</summary>
[ExcludeFromCodeCoverage]
public sealed record StepChartRowRecord(decimal Time, int PanelMask, int LeftFootMask, int Quant, decimal? Beat);

[ExcludeFromCodeCoverage]
public sealed record StepChartHoldRecord(int Panel, decimal Start, decimal End, bool IsLeftFoot);

[ExcludeFromCodeCoverage]
public sealed record StepChartSegmentRecord(decimal Start, decimal End, decimal? Enps);

[ExcludeFromCodeCoverage]
public sealed record StepChartRangeRecord(decimal Start, decimal End);
