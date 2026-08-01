namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>What an undo did, or why it did nothing.</summary>
public enum ScoreSessionUndoOutcome
{
    Undone,

    /// <summary>No such session, or it belongs to somebody else.</summary>
    NotFound,

    /// <summary>Older than the floor: we did not record when those scores arrived.</summary>
    TooOld
}

[ExcludeFromCodeCoverage]
public sealed record ScoreSessionUndoResult(
    ScoreSessionUndoOutcome Outcome,
    int ChartsRestored = 0,
    int ChartsRemoved = 0);
