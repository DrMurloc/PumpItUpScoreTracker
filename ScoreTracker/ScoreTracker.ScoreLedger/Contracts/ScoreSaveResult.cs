namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     What one saved score changed, the way an announcement counts it: a chart that became a pass, a
///     pass that went up (and the score it replaced), or nothing worth announcing — a walk-off, a stage
///     break, a score that did not beat the record, a plate-only or lowered record.
///     <para>
///         A caller that announces for itself (<see cref="Commands.UpdatePhoenixBestAttemptCommand.DeferAnnouncement" />)
///         keeps these and hands them to <see cref="Commands.AnnounceScoreChangesCommand" /> once it has
///         saved everything. The default value is <see cref="ScoreSaveChange.None" />, so a result nobody
///         set reads as "nothing changed" rather than as a change.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public readonly record struct ScoreSaveResult(Guid ChartId, ScoreSaveChange Change, int? UpscoredFrom = null);

public enum ScoreSaveChange
{
    None = 0,
    NewPass,
    Upscore
}
