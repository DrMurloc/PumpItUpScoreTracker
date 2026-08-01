namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     What undoing a session would do, for the confirm dialog. The second count is the one that
///     matters: charts with no earlier play cannot be put back, only removed.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ScoreSessionUndoPreview(
    ScoreSessionRecord Session,
    int ChartsRestored,
    int ChartsRemoved,
    int PlaysRemoved);
