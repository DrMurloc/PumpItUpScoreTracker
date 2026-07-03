namespace ScoreTracker.Identity.Contracts;

[ExcludeFromCodeCoverage]
public sealed record ExternalLinkOutcome(ExternalLinkResult Result, Guid? ConflictingUserId)
{
}
