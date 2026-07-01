namespace ScoreTracker.EventCompetition.Contracts.Messages;

/// <summary>
///     Daily Hangfire trigger: schedule (or immediately fire) the next March of Murlocs
///     cycle. The consumer owns the decision — if the current MoM is still active it
///     schedules a delayed <see cref="CycleMoMCommand" /> instead of cycling.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TryScheduleMoMCommand
{
}
