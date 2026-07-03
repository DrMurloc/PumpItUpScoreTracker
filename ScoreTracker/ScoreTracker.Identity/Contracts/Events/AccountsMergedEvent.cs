namespace ScoreTracker.Identity.Contracts.Events;

/// <summary>
///     Two accounts merged: the retired account's sign-in methods now point at the survivor
///     and the retired account is hidden. Its data is untouched until the grace window ends
///     (AccountPurgeStartedEvent).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record AccountsMergedEvent(Guid SurvivorUserId, Guid RetiredUserId)
{
}
