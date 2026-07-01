namespace ScoreTracker.EventCompetition.Contracts.Messages;

/// <summary>
///     Close out the expired March of Murlocs tournaments and create the next season's
///     pair (Singles/Doubles). Idempotent: a future-dated MoM short-circuits the cycle.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CycleMoMCommand
{
}
