namespace ScoreTracker.Domain.Records
{
    /// <summary>
    ///     The raw step content of one piucenter chart page — the arrays the aggregate parse
    ///     summarizes and previously discarded (docs/design/step-chart-failure-map.md D4): every
    ///     arrow with its limb, every hold, the authored per-hold tick tallies, the segment
    ///     spans, and the generator's own record of which .ssc it read. Times are the
    ///     generator's seconds throughout.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record PiuCenterChartSteps(
        IReadOnlyList<StepArrow> Taps,
        IReadOnlyList<PiuCenterStepHold> Holds,
        IReadOnlyList<PiuCenterTickSpan> TickSpans,
        IReadOnlyList<PiuCenterSegmentSpan> Segments,
        IReadOnlyList<PiuCenterRangeSpan> RangesOfInterest,
        string? SscFile,
        string? StepsType,
        int? Meter);

    [ExcludeFromCodeCoverage]
    public sealed record PiuCenterStepHold(int Panel, decimal Start, decimal End, string Limb);

    [ExcludeFromCodeCoverage]
    public sealed record PiuCenterTickSpan(decimal Start, decimal End, int Count);

    [ExcludeFromCodeCoverage]
    public sealed record PiuCenterSegmentSpan(decimal Start, decimal End, decimal? Enps);

    [ExcludeFromCodeCoverage]
    public sealed record PiuCenterRangeSpan(decimal Start, decimal End);
}
