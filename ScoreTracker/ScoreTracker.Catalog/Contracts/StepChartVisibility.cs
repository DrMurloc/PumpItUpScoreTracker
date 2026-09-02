namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     What a chart's step file has earned on one mix
///     (docs/design/step-chart-failure-map.md D8/D9). <see cref="Excluded" /> is
///     stepfile-precision §7 computed at ingest — the file is provably not the shipped chart,
///     so no section renders at all; <see cref="StepsOnly" /> renders the strip but no failure
///     pins (taps are the trustworthy half, positions are not); <see cref="Full" /> is the
///     within-2% population where a death's judgement count indexes honestly into the timeline.
/// </summary>
public enum StepChartVisibility
{
    Excluded = 0,
    StepsOnly = 1,
    Full = 2
}
