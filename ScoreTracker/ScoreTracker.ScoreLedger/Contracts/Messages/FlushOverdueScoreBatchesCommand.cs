namespace ScoreTracker.ScoreLedger.Contracts.Messages;

/// <summary>
///     Sweeps score work that should already have announced itself and has not.
///     <para>
///         Two consumers, deliberately: ScoreLedger drains batches still sitting in the
///         accumulator past their deadline, and OfficialMirror replays sessions whose batch is
///         gone entirely. They cover different halves of the same symptom and neither subsumes
///         the other — see <c>docs/design/import-restart-recovery.md</c> §4.3.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record FlushOverdueScoreBatchesCommand
{
}
