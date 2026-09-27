namespace ScoreTracker.ScoreLedger.Contracts.Messages;

/// <summary>
///     Sweeps score work that should already have announced itself and has not.
///     <para>
///         ScoreLedger drains typed-entry batches still sitting in the accumulator past their
///         deadline, then publishes <see cref="Events.OverdueScoreBatchesFlushedEvent" />, on which
///         the sittings sweep closes sittings whose scheduled close was lost and OfficialMirror
///         replays the sessions of import runs that failed after saving
///         (<c>docs/design/import-restart-recovery.md</c> §0).
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record FlushOverdueScoreBatchesCommand
{
}
