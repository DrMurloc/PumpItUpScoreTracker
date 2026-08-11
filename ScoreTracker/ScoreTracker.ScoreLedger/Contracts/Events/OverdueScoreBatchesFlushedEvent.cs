namespace ScoreTracker.ScoreLedger.Contracts.Events;

/// <summary>
///     The in-memory half of the recovery sweep has finished: every batch that was sitting past
///     its deadline has been taken and announced.
///     <para>
///         Published so the journal-replay half can run <em>after</em> it rather than beside it.
///         The two are in scope for the same sessions on the tick that matters, and a session is
///         only marked processed when its capture chain ends — so a replay running concurrently
///         would still see it unprocessed and announce the same scores twice.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OverdueScoreBatchesFlushedEvent(DateTimeOffset FlushedAt)
{
}
