namespace ScoreTracker.Domain.Records;

/// <summary>
///     A batch the accumulator handed over because its deadline had passed, with the player it
///     belongs to — <see cref="PendingScoreBatch" /> carries the mix and the session but not the
///     user, and the announcement needs all three.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DueScoreBatch(Guid UserId, PendingScoreBatch Batch)
{
}
