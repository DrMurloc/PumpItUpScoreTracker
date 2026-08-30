namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     One imported run that ended mid-chart: the judgements it produced (its position on the
///     timeline), whether the life bar provably could not have ended it (the Stage Pass
///     series), whether it belongs to the viewer who asked, and — where the solver could name
///     them — the plate and grade the run's last judgement put out of reach, as stored
///     (full names; docs/design/pass-command-detection.md D31/D32).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartStageBreakRecord(int Judged, bool IsNonLifebarBreak, bool IsViewer,
    string? PassPlate = null, string? PassGrade = null);
