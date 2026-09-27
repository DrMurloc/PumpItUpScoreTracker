namespace ScoreTracker.ScoreLedger.Contracts.Events;

/// <summary>
///     The in-memory half of the five-minute sweep has finished: every batch that was sitting past
///     its deadline has been taken and announced.
///     <para>
///         Published so the journal-replay halves — the sittings sweep, and OfficialMirror's replay
///         of failed import runs — run <em>after</em> it rather than beside it, off one tick.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OverdueScoreBatchesFlushedEvent(DateTimeOffset FlushedAt)
{
}
