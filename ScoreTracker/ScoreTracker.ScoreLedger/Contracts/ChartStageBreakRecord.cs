namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     One imported run that ended mid-chart: the judgements it produced (its position on the
///     timeline), whether the life bar provably could not have ended it (the Stage Pass
///     series), and whether it belongs to the viewer who asked.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartStageBreakRecord(int Judged, bool IsNonLifebarBreak, bool IsViewer);
